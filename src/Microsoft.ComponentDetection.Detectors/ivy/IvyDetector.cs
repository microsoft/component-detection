#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Ivy;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

/// <summary>
/// Detector for Maven components declared in ivy.xml files for Java projects that are built using Apache Ant
/// and Apache Ivy.
/// </summary>
/// <remarks>
/// Ivy can use artifact repositories that are in the traditional Ivy layout or m2compatible repositories.  The
/// m2compatible repositories use the Maven three-coordinate system GAV (group, artifact, version) to identify
/// components, which corresponds directly to Ivy coordinates (org, name, rev).  The project has its own
/// assigned organisation, and any dependencies with the same organisation are taken as first party dependencies
/// and ignored by the detector.
///
/// This detector relies on Apache Ant being available in the PATH, because it needs to run Ivy's resolver to
/// find transitive dependencies (and these in turn require a JDK installed).  The ivy.xml file and (if it exists)
/// ivysettings.xml file from the same directory are copied to a temporary directory, along with a synthetic
/// build.xml and the Java source code of a custom Ant task.  The detector then invokes Ant in this temporary
/// directory to resolve the dependencies and write out a file for this detector to parse.  Note that for this
/// to work, it requires ivy.xml and ivysettings.xml to be self-contained: if they rely on any properties defined
/// in the project's build.xml, or if they use any file inclusion mechanism, it will fail.
///
/// The file written out by the custom Ant task is a simple JSON file representing a series of calls to be made to
/// the <see cref="ISingleFileComponentRecorder.RegisterUsage(DetectedComponent, bool, string, bool?, DependencyScope?, string)"/> method.
/// </remarks>
public class IvyDetector : FileComponentDetector, IExperimentalDetector
{
    internal const string PrimaryCommand = "ant.bat";

    internal const string AntVersionArgument = "-version";

    internal static readonly string[] AdditionalValidCommands = ["ant"];

    private readonly ICommandLineInvocationService commandLineInvocationService;

    public IvyDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        ILogger<IvyDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.commandLineInvocationService = commandLineInvocationService;
        this.Logger = logger;
    }

    public override string Id => "Ivy";

    public override IList<string> SearchPatterns => ["ivy.xml"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Maven];

    public override int Version => 2;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Maven)];

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        if (await this.IsAntLocallyAvailableAsync())
        {
            return processRequests;
        }

        this.Logger.LogDebug("Skipping Ivy detection as ant is not available in the local PATH.");
        return Enumerable.Empty<ProcessRequest>().ToObservable();
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var ivyXmlFile = processRequest.ComponentStream;

        var dirName = Path.GetDirectoryName(ivyXmlFile.Location);
        var ivySettingsFilePath = Path.Combine(dirName, "ivysettings.xml");
        if (File.Exists(ivyXmlFile.Location))
        {
            if (File.Exists(ivySettingsFilePath))
            {
                this.Logger.LogInformation("Processing {IvyXmlFileLocation} and ivysettings.xml.", ivyXmlFile.Location);
                await this.ProcessIvyAndIvySettingsFilesAsync(singleFileComponentRecorder, ivyXmlFile.Location, ivySettingsFilePath);
            }
            else
            {
                this.Logger.LogInformation("Processing {IvyXmlFile}.", ivyXmlFile.Location);
                await this.ProcessIvyAndIvySettingsFilesAsync(singleFileComponentRecorder, ivyXmlFile.Location, null);
            }
        }
        else
        {
            this.Logger.LogError("File {IvyXmlFileLocation} passed to OnFileFound, but does not exist!", ivyXmlFile.Location);
        }
    }

    private static MavenComponent JsonGavToComponent(JToken gav)
    {
        if (gav == null)
        {
            return null;
        }

        return new MavenComponent(
            gav.Value<string>("g"),
            gav.Value<string>("a"),
            gav.Value<string>("v"));
    }

    private async Task ProcessIvyAndIvySettingsFilesAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        string ivyXmlFile,
        string ivySettingsXmlFile)
    {
        try
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            this.Logger.LogDebug("Preparing temporary Ivy project in {WorkingDirectory}", workingDirectory);
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }

            this.InitTemporaryAntProject(workingDirectory, ivyXmlFile, ivySettingsXmlFile);
            if (await this.RunAntToDetectDependenciesAsync(workingDirectory))
            {
                var instructionsFile = Path.Combine(workingDirectory, "target", "RegisterUsage.json");
                this.RegisterUsagesFromFile(singleFileComponentRecorder, instructionsFile);
            }

            Directory.Delete(workingDirectory, recursive: true);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Exception occurred processing {FileName} ", ivyXmlFile);
        }
    }

    private void InitTemporaryAntProject(string workingDirectory, string ivyXmlFile, string ivySettingsXmlFile)
    {
        Directory.CreateDirectory(workingDirectory);
        File.Copy(ivyXmlFile, Path.Combine(workingDirectory, "ivy.xml"));
        if (ivySettingsXmlFile != null)
        {
            File.Copy(ivySettingsXmlFile, Path.Combine(workingDirectory, "ivysettings.xml"));
        }

        var assembly = Assembly.GetExecutingAssembly();

        using (var fileIn = assembly.GetManifestResourceStream("Microsoft.ComponentDetection.Detectors.ivy.Resources.build.xml"))
        using (var fileOut = File.Create(Path.Combine(workingDirectory, "build.xml")))
        {
            fileIn.CopyTo(fileOut);
        }

        Directory.CreateDirectory(Path.Combine(workingDirectory, "java-src"));
        using (var fileIn = assembly.GetManifestResourceStream("Microsoft.ComponentDetection.Detectors.ivy.Resources.java_src.IvyComponentDetectionAntTask.java"))
        using (var fileOut = File.Create(Path.Combine(workingDirectory, "java-src", "IvyComponentDetectionAntTask.java")))
        {
            fileIn.CopyTo(fileOut);
        }
    }

    private async Task<bool> IsAntLocallyAvailableAsync()
    {
        // Note: calling CanCommandBeLocated populates a cache of valid commands.  If it is not called before ExecuteCommand,
        // ExecuteCommand calls CanCommandBeLocated with no arguments, which fails.
        return await this.commandLineInvocationService.CanCommandBeLocatedAsync(PrimaryCommand, AdditionalValidCommands, AntVersionArgument);
    }

    private async Task<bool> RunAntToDetectDependenciesAsync(string workingDirectory)
    {
        var ret = false;
        this.Logger.LogDebug("Executing command `ant resolve-dependencies` in directory {WorkingDirectory}", workingDirectory);
        var result = await this.commandLineInvocationService.ExecuteCommandAsync(PrimaryCommand, additionalCandidateCommands: AdditionalValidCommands, "-buildfile", workingDirectory, "resolve-dependencies");
        if (result.ExitCode == 0)
        {
            this.Logger.LogDebug("Ant command succeeded");
            ret = true;
        }
        else
        {
            this.Logger.LogError("Ant command failed with return code {ExitCode}", result.ExitCode);
        }

        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            this.Logger.LogDebug("Ant command wrote nothing to stdout.");
        }
        else
        {
            this.Logger.LogDebug("Ant command stdout: {AntCmdStdOut}", result.StdOut);
        }

        if (string.IsNullOrWhiteSpace(result.StdErr))
        {
            this.Logger.LogDebug("Ant command wrote nothing to stderr.");
        }
        else
        {
            this.Logger.LogWarning("Ant command stderr: {AntCmdStdErr}", result.StdErr);
        }

        return ret;
    }

    private void RegisterUsagesFromFile(ISingleFileComponentRecorder singleFileComponentRecorder, string instructionsFile)
    {
        var instructionsJson = JObject.Parse(File.ReadAllText(instructionsFile));
        var instructionsList = (JContainer)instructionsJson["RegisterUsage"];
        foreach (var dep in instructionsList)
        {
            var component = JsonGavToComponent(dep["gav"]);
            var isDevDependency = dep.Value<bool>("DevelopmentDependency");
            var parentComponent = JsonGavToComponent(dep["parent_gav"]);
            var isResolved = dep.Value<bool>("resolved");
            if (isResolved)
            {
                singleFileComponentRecorder.RegisterUsage(
                    detectedComponent: new DetectedComponent(component),
                    isExplicitReferencedDependency: parentComponent == null,
                    parentComponentId: parentComponent?.Id,
                    isDevelopmentDependency: isDevDependency);
            }
            else
            {
                this.Logger.LogWarning("Dependency \"{MavenComponentId}\" could not be resolved by Ivy, and so has not been recorded by Component Detection.", component.Id);
                singleFileComponentRecorder.RegisterPackageParseFailure(component.Id);
            }
        }
    }
}

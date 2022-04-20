using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Newtonsoft.Json.Linq;

namespace Microsoft.ComponentDetection.Detectors.Ivy
{
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
    /// the <see cref="ISingleFileComponentRecorder.RegisterUsage(DetectedComponent, bool, string, bool?, DependencyScope?)"/> method.
    /// </remarks>
    [Export(typeof(IComponentDetector))]
    public class IvyDetector : FileComponentDetector, IExperimentalDetector
    {
        public override string Id => "Ivy";

        public override IList<string> SearchPatterns => new List<string> { "ivy.xml" };

        public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Maven };

        public override int Version => 2;

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Maven) };

        internal const string PrimaryCommand = "ant.bat";

        internal static readonly string[] AdditionalValidCommands = { "ant" };

        internal const string AntVersionArgument = "-version";

        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        protected override async Task<IObservable<ProcessRequest>> OnPrepareDetection(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
        {
            if (await IsAntLocallyAvailableAsync())
            {
                return processRequests;
            }

            Logger.LogVerbose("Skipping Ivy detection as ant is not available in the local PATH.");
            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var ivyXmlFile = processRequest.ComponentStream;

            var dirName = Path.GetDirectoryName(ivyXmlFile.Location);
            var ivySettingsFilePath = Path.Combine(dirName, "ivysettings.xml");
            if (File.Exists(ivyXmlFile.Location))
            {
                if (File.Exists(ivySettingsFilePath))
                {
                    Logger.LogInfo($"Processing {ivyXmlFile.Location} and ivysettings.xml.");
                    await ProcessIvyAndIvySettingsFilesAsync(singleFileComponentRecorder, ivyXmlFile.Location, ivySettingsFilePath);
                }
                else
                {
                    Logger.LogInfo($"Processing {ivyXmlFile.Location}.");
                    await ProcessIvyAndIvySettingsFilesAsync(singleFileComponentRecorder, ivyXmlFile.Location, null);
                }
            }
            else
            {
                Logger.LogError($"File {ivyXmlFile.Location} passed to OnFileFound, but does not exist!");
            }
        }

        private async Task ProcessIvyAndIvySettingsFilesAsync(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            string ivyXmlFile,
            string ivySettingsXmlFile)
        {
            try
            {
                string workingDirectory = Path.Combine(Path.GetTempPath(), "ComponentDetection_Ivy");
                Logger.LogVerbose($"Preparing temporary Ivy project in {workingDirectory}");
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }

                InitTemporaryAntProject(workingDirectory, ivyXmlFile, ivySettingsXmlFile);
                if (await RunAntToDetectDependenciesAsync(workingDirectory))
                {
                    string instructionsFile = Path.Combine(workingDirectory, "target", "RegisterUsage.json");
                    RegisterUsagesFromFile(singleFileComponentRecorder, instructionsFile);
                }

                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (Exception e)
            {
                Logger.LogError("Exception occurred during Ivy file processing: " + e);

                // If something went wrong, just ignore the file
                Logger.LogFailedReadingFile(ivyXmlFile, e);
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

            using (Stream fileIn = assembly.GetManifestResourceStream("Microsoft.ComponentDetection.Detectors.ivy.Resources.build.xml"))
            using (FileStream fileOut = File.Create(Path.Combine(workingDirectory, "build.xml")))
            {
                fileIn.CopyTo(fileOut);
            }

            Directory.CreateDirectory(Path.Combine(workingDirectory, "java-src"));
            using (Stream fileIn = assembly.GetManifestResourceStream("Microsoft.ComponentDetection.Detectors.ivy.Resources.java_src.IvyComponentDetectionAntTask.java"))
            using (FileStream fileOut = File.Create(Path.Combine(workingDirectory, "java-src", "IvyComponentDetectionAntTask.java")))
            {
                fileIn.CopyTo(fileOut);
            }
        }

        private async Task<bool> IsAntLocallyAvailableAsync()
        {
            // Note: calling CanCommandBeLocated populates a cache of valid commands.  If it is not called before ExecuteCommand,
            // ExecuteCommand calls CanCommandBeLocated with no arguments, which fails.
            return await CommandLineInvocationService.CanCommandBeLocated(PrimaryCommand, AdditionalValidCommands, AntVersionArgument);
        }

        private async Task<bool> RunAntToDetectDependenciesAsync(string workingDirectory)
        {
            bool ret = false;
            Logger.LogVerbose($"Executing command `ant resolve-dependencies` in directory {workingDirectory}");
            CommandLineExecutionResult result = await CommandLineInvocationService.ExecuteCommand(PrimaryCommand, additionalCandidateCommands: AdditionalValidCommands, "-buildfile", workingDirectory, "resolve-dependencies");
            if (result.ExitCode == 0)
            {
                Logger.LogVerbose("Ant command succeeded");
                ret = true;
            }
            else
            {
                Logger.LogError($"Ant command failed with return code {result.ExitCode}");
            }

            if (string.IsNullOrWhiteSpace(result.StdOut))
            {
                Logger.LogVerbose("Ant command wrote nothing to stdout.");
            }
            else
            {
                Logger.LogVerbose("Ant command stdout:\n" + result.StdOut);
            }

            if (string.IsNullOrWhiteSpace(result.StdErr))
            {
                Logger.LogVerbose("Ant command wrote nothing to stderr.");
            }
            else
            {
                Logger.LogWarning("Ant command stderr:\n" + result.StdErr);
            }

            return ret;
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

        private void RegisterUsagesFromFile(ISingleFileComponentRecorder singleFileComponentRecorder, string instructionsFile)
        {
            JObject instructionsJson = JObject.Parse(File.ReadAllText(instructionsFile));
            JContainer instructionsList = (JContainer)instructionsJson["RegisterUsage"];
            foreach (JToken dep in instructionsList)
            {
                MavenComponent component = JsonGavToComponent(dep["gav"]);
                bool isDevDependency = dep.Value<bool>("DevelopmentDependency");
                MavenComponent parentComponent = JsonGavToComponent(dep["parent_gav"]);
                bool isResolved = dep.Value<bool>("resolved");
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
                    Logger.LogWarning($"Dependency \"{component.Id}\" could not be resolved by Ivy, and so has not been recorded by Component Detection.");
                }
            }
        }
    }
}

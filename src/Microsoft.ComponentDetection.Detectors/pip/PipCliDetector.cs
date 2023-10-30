namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// A Pip CLI detector that uses the pip inspect command to detect Pip components.
/// </summary>
public class PipCliDetector : FileComponentDetector, IExperimentalDetector
{
    private readonly ICommandLineInvocationService cliService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipCliDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The component stream enumerable factory.</param>
    /// <param name="walkerFactory">The walker factory.</param>
    /// <param name="cliService">The command line invocation service.</param>
    /// <param name="logger">The logger.</param>
    public PipCliDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService cliService,
        ILogger<PipCliDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.cliService = cliService;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override string Id => "PipCli";

    /// <inheritdoc />
    public override IEnumerable<string> Categories { get; } = new[] { "Pip" };

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Pip };

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = new[] { "requirements.txt", "setup.py" };

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var componentStream = processRequest.ComponentStream;

        using var record = new PipInspectTelemetryRecord();

        try
        {
            if (!await this.cliService.CanCommandBeLocatedAsync("pip", null))
            {
                record.PipNotFound = true;
                this.Logger.LogWarning("Could not locate pip command. Skipping Pip CLI detection");
                return;
            }

            // create a virtual environment
            // `python -m venv venv`
            // activate the virtual environment
            // `venv\Scripts\activate.bat` or `source venv/bin/activate`
            var cliResult = await this.cliService.ExecuteCommandAsync(
                "pip",
                null,
                "inspect",
                "--path",
                componentStream.Location);

            if (cliResult.ExitCode < 0)
            {
                this.Logger.LogWarning("`pip inspect` failed with {Location}. Ensure the requirements.txt is up to date. stderr: {StdErr}", processRequest.ComponentStream.Location, cliResult.StdErr);
                return;
            }

            var metadata = PipInspect.FromJson(cliResult.StdOut);

            if (metadata.Version != 1)
            {
                this.Logger.LogWarning("Unsupported pip inspect version {Version}. Skipping Pip CLI detection", metadata.Version);
                return;
            }

            var graph = BuildGraph(metadata);
            var root = metadata.Installed.FirstOrDefault().Metadata.Name;

            this.TraverseAndRecordComponents(processRequest.SingleFileComponentRecorder, componentStream.Location, graph, root, null, null);
        }
        catch (InvalidOperationException e)
        {
            this.Logger.LogWarning(e, "Failed attempting to call `pip` with file: {Location}", processRequest.ComponentStream.Location);
        }
        finally
        {
            // cleanup the virtual environment
            // `deactivate`
        }
    }

    private static Dictionary<string, Installed> BuildGraph(PipInspect pipInpsect) => pipInpsect.Installed.ToDictionary(x => x.Metadata.Name);

    private static (string Name, string Version) ParseNameAndVersion(string nameAndVersion)
    {
        var parts = nameAndVersion.Split(' ');
        return (parts[0], parts[1]);
    }

    private void TraverseAndRecordComponents(
        ISingleFileComponentRecorder recorder,
        string location,
        IReadOnlyDictionary<string, Installed> graph,
        string id,
        DetectedComponent parent,
        string depInfo,
        bool explicitlyReferencedDependency = false)
    {
        try
        {
            // var isDevelopmentDependency = depInfo?.DepKinds.Any(x => x.Kind is Kind.Dev or Kind.Build) ?? false;
            var (name, version) = ParseNameAndVersion(id);
            var detectedComponent = new DetectedComponent(new PipComponent(name, version));

            recorder.RegisterUsage(
                detectedComponent,
                explicitlyReferencedDependency,
                /* isDevelopmentDependency: isDevelopmentDependency, */
                parentComponentId: parent?.Component.Id);

            if (!graph.TryGetValue(id, out var node))
            {
                this.Logger.LogWarning("Could not find {Id} at {Location} in pip inspect output", id, location);
                return;
            }

            foreach (var dep in node.Metadata.RequiresDist)
            {
                this.TraverseAndRecordComponents(recorder, location, graph, dep, detectedComponent, dep, parent == null);
            }
        }
        catch (IndexOutOfRangeException e)
        {
            this.Logger.LogWarning(e, "Could not parse {Id} at {Location}", id, location);
            recorder.RegisterPackageParseFailure(id);
        }
    }
}

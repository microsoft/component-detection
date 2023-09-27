namespace Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;

/// <summary>
/// Validating the Rust CLI detector against the Rust crate detector.
/// </summary>
public class RustCliDetectorExperiment : IExperimentConfiguration
{
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private bool cargoCliAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustCliDetectorExperiment"/> class.
    /// </summary>
    /// <param name="commandLineInvocationService">The command line invocation service.</param>
    public RustCliDetectorExperiment(ICommandLineInvocationService commandLineInvocationService) => this.commandLineInvocationService = commandLineInvocationService;

    /// <inheritdoc />
    public string Name => "RustCliDetector";

    /// <inheritdoc/>
    public bool IsInControlGroup(IComponentDetector componentDetector) => componentDetector is RustCrateDetector;

    /// <inheritdoc/>
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is RustCliDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => this.cargoCliAvailable;

    /// <inheritdoc />
    public async Task InitAsync() => this.cargoCliAvailable = await this.commandLineInvocationService.CanCommandBeLocatedAsync("cargo", null);
}

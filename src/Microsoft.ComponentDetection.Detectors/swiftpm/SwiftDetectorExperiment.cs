namespace Microsoft.ComponentDetection.Detectors.Swift;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Records Swift Package Manager discoveries against an empty baseline.
/// </summary>
public class SwiftDetectorExperiment : IExperimentConfiguration
{
    /// <inheritdoc />
    public string Name => "SwiftDetectorExperiment";

    /// <inheritdoc />
    public Task InitAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => false;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is SwiftResolvedComponentDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}

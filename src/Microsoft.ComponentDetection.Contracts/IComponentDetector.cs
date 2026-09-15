#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Basic interface required for something to satisfy component detection.
/// If you are writing a File based component detector, you may prefer the<see cref="FileComponentDetector" /> class.
/// </summary>
public interface IComponentDetector
{
    /// <summary>Gets the id of the detector. Should be unique to the detector using a "namespace"-like prefix for your detectors is recommended. </summary>
    string Id { get; }

    /// <summary>
    /// Gets the set of categories this detector is a member of.
    /// Names of the <see cref="DetectorClass"/> enumeration comprise some of the built in categories.
    /// If the category "All" is specified, the detector will always run.
    /// </summary>
    IEnumerable<string> Categories { get; }

    /// <summary>
    /// Gets the set of supported component type this detector is a member of. Names of the <see cref="DetectorClass"/> enumeration comprise some of the built in component type.
    /// </summary>
    IEnumerable<ComponentType> SupportedComponentTypes { get; }

    /// <summary>
    /// Gets the version of the component detector.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets a value indicating whether this detector needs automatic root dependency calculation or is going to be specified as part of RegisterUsage.
    /// </summary>
    bool NeedsAutomaticRootDependencyCalculation { get; }

    /// <summary>
    ///  Run the detector and return the result set of components found.
    /// </summary>
    /// <returns> Awaitable task with result of components found. </returns>
    Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Component detectors implementing this interface are, by default, off. This is used during composition to opt detectors out of being on by default.
/// If opted in, they should behave like a normal detector.
/// </summary>
public interface IDefaultOffComponentDetector : IComponentDetector
{
}

/// <summary>
/// Component detectors implementing this interface are in an experimental state.
/// The detector processing service guarantees that:
///     They should NOT return their components as part of the scan result or be allowed to run too long (e.g. 4 min or less).
///     They SHOULD submit telemetry about how they ran.
/// If opted in, they should behave like a normal detector.
/// </summary>
public interface IExperimentalDetector : IComponentDetector
{
}

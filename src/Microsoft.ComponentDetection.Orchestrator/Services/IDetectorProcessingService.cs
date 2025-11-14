namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Commands;

/// <summary>
/// Defines a service for processing component detectors during a scan operation.
/// </summary>
public interface IDetectorProcessingService
{
    /// <summary>
    /// Processes the specified detectors asynchronously based on the provided scan settings and detector restrictions.
    /// </summary>
    /// <param name="settings">The scan settings that configure how the detection process should be executed.</param>
    /// <param name="detectors">The collection of component detectors to be processed.</param>
    /// <param name="detectorRestrictions">The restrictions that determine which detectors should be included or excluded from processing.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="DetectorProcessingResult"/> with information about the detection process outcome.</returns>
    public Task<DetectorProcessingResult> ProcessDetectorsAsync(
        ScanSettings settings,
        IEnumerable<IComponentDetector> detectors,
        DetectorRestrictions detectorRestrictions,
        CancellationToken cancellationToken = default);
}

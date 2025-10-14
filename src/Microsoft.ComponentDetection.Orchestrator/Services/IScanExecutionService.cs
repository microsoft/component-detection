#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.Commands;

/// <summary>
/// Defines a service responsible for executing component detection scans.
/// </summary>
public interface IScanExecutionService
{
    /// <summary>
    /// Executes a scan asynchronously based on the provided scan settings.
    /// </summary>
    /// <param name="settings">The scan settings that configure how the scan should be executed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous scan operation. The task result contains the <see cref="ScanResult"/> with information about the scan execution.</returns>
    public Task<ScanResult> ExecuteScanAsync(ScanSettings settings, CancellationToken cancellationToken = default);
}

#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>
/// Results object for a component scan.
/// </summary>
public class IndividualDetectorScanResult
{
    /// <summary>
    /// Gets or sets the result code for the scan, indicating Success or Failure.
    /// </summary>
    public ProcessingResultCode ResultCode { get; set; }

    /// <summary>
    /// Gets or sets the list of containers found during the scan.
    /// </summary>
    public IEnumerable<ContainerDetails> ContainerDetails { get; set; } = [];

    /// <summary>
    /// Gets or sets any additional telemetry details for the scan.
    /// </summary>
    public Dictionary<string, string> AdditionalTelemetryDetails { get; set; }
}

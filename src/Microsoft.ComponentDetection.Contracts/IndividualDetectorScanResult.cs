using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.Contracts;

/// <summary>Results object for a component scan.</summary>
public class IndividualDetectorScanResult
{
    /// <summary>Gets or sets the result code for the scan, indicating Success or Failure.</summary>
    public ProcessingResultCode ResultCode { get; set; }

    public IEnumerable<ContainerDetails> ContainerDetails { get; set; } = Enumerable.Empty<ContainerDetails>();

    public Dictionary<string, string> AdditionalTelemetryDetails { get; set; }
}

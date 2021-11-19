using System;

namespace Microsoft.ComponentDetection.Common.Telemetry.Attributes
{
    /// <summary>
    /// Denotes that a telemetry property should be treated as a Metric (numeric) value
    ///
    /// It is up to the implementing Telemetry Service to interpret this value.
    /// </summary>
    public class MetricAttribute : Attribute
    {
    }
}

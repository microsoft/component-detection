namespace Microsoft.ComponentDetection.Common.Telemetry.Attributes;

using System;

/// <summary>
/// Denotes that a telemetry property should be treated as a Metric (numeric) value
///
/// It is up to the implementing Telemetry Service to interpret this value.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MetricAttribute : Attribute
{
}

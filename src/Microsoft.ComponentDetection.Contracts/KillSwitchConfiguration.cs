#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Represents the configuration for the kill switch.
/// </summary>
public class KillSwitchConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the detection should be stopped.
    /// </summary>
    public bool IsDetectionStopped { get; set; }

    /// <summary>
    /// Gets or sets the reason for stopping the detection.
    /// </summary>
    public string ReasonForStoppingDetection { get; set; }
}

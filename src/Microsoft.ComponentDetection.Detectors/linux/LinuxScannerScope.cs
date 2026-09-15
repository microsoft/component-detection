namespace Microsoft.ComponentDetection.Detectors.Linux;

/// <summary>
/// Defines the scope for scanning Linux container images.
/// </summary>
public enum LinuxScannerScope
{
    /// <summary>
    /// Scan files from all layers of the image.
    /// </summary>
    AllLayers,

    /// <summary>
    /// Scan only the files accessible from the final layer of the image.
    /// </summary>
    Squashed,
}

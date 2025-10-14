#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxContainerDetectorLayerAwareness : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxContainerDetectorLayerAwareness";

    public string BaseImageRef { get; set; }

    public string BaseImageDigest { get; set; }

    public int? BaseImageLayerCount { get; set; }

    public int? LayerCount { get; set; }

    public string BaseImageLayerMessage { get; set; }
}

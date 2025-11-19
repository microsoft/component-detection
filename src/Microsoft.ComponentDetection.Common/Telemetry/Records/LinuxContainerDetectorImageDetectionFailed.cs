#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

public class LinuxContainerDetectorImageDetectionFailed : BaseDetectionTelemetryRecord
{
    public override string RecordName => "LinuxContainerDetectorImageDetectionFailed";

    public string ImageId { get; set; }

    public string Message { get; set; }

    public string ExceptionType { get; set; }

    public string StackTrace { get; set; }
}

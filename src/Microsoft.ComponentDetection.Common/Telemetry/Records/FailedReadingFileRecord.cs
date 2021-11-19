namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public class FailedReadingFileRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "FailedReadingFile";

        public string FilePath { get; set; }

        public string ExceptionMessage { get; set; }

        public string StackTrace { get; set; }
    }
}

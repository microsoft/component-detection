using System;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public class CommandLineInvocationTelemetryRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "CommandLineInvocation";

        public string PathThatWasRan { get; set; }

        public string Parameters { get; set; }

        public int? ExitCode { get; set; }

        public string StandardError { get; set; }

        public string UnhandledException { get; set; }

        internal void Track(CommandLineExecutionResult result, string path, string parameters)
        {
            ExitCode = result.ExitCode;
            StandardError = result.StdErr;
            TrackCommon(path, parameters);
        }

        internal void Track(Exception ex, string path, string parameters)
        {
            ExitCode = -1;
            UnhandledException = ex.ToString();
            TrackCommon(path, parameters);
        }

        private void TrackCommon(string path, string parameters)
        {
            PathThatWasRan = path;
            Parameters = parameters;
            StopExecutionTimer();
        }
    }
}

using System;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records
{
    public class BcdeExecutionTelemetryRecord : BaseDetectionTelemetryRecord
    {
        public override string RecordName => "BcdeExecution";

        public string Command { get; set; }

        public int? ExitCode { get; set; }

        public int? HiddenExitCode { get; set; }

        public string UnhandledException { get; set; }

        public string Arguments { get; set; }

        public string ErrorMessage { get; set; }

        public string AgentOSMeaningfulDetails { get; set; }

        public string AgentOSDescription { get; set; }

        public static TReturn Track<TReturn>(Func<BcdeExecutionTelemetryRecord, TReturn> functionToTrack, bool terminalRecord = false)
        {
            using var record = new BcdeExecutionTelemetryRecord();

            try
            {
                return functionToTrack(record);
            }
            catch (Exception ex)
            {
                record.UnhandledException = ex.ToString();
                throw;
            }
            finally
            {
                record.Dispose();
                if (terminalRecord && !(record.Command?.Equals("help", StringComparison.InvariantCultureIgnoreCase) ?? false))
                {
                    TelemetryRelay.Instance.Shutdown();
                }
            }
        }
    }
}

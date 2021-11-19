using System;
using System.Collections.Concurrent;
using System.Composition;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.ComponentDetection.Common.Telemetry
{
    [Export(typeof(ITelemetryService))]
    [TelemetryService(nameof(CommandLineTelemetryService))]
    internal class CommandLineTelemetryService : ITelemetryService
    {
        [Import]
        public ILogger Logger { get; set; }

        [Import]
        public IFileWritingService FileWritingService { get; set; }

        public const string TelemetryRelativePath = "ScanTelemetry_{timestamp}.json";

        private TelemetryMode telemetryMode = TelemetryMode.Production;

        private static ConcurrentQueue<JObject> records = new ConcurrentQueue<JObject>();

        public void Flush()
        {
            FileWritingService.WriteFile(TelemetryRelativePath, JsonConvert.SerializeObject(records));
        }

        public void PostRecord(IDetectionTelemetryRecord record)
        {
            if (telemetryMode != TelemetryMode.Disabled)
            {
                var jsonRecord = JObject.FromObject(record);
                jsonRecord.Add("Timestamp", DateTime.UtcNow);
                jsonRecord.Add("CorrelationId", TelemetryConstants.CorrelationId);

                records.Enqueue(jsonRecord);

                if (telemetryMode == TelemetryMode.Debug)
                {
                    Logger.LogInfo(jsonRecord.ToString());
                }
            }
        }

        public void SetMode(TelemetryMode mode)
        {
            telemetryMode = mode;
        }
    }
}

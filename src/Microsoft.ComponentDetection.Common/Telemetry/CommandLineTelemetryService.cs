namespace Microsoft.ComponentDetection.Common.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Composition;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Export(typeof(ITelemetryService))]
[TelemetryService(nameof(CommandLineTelemetryService))]
internal class CommandLineTelemetryService : ITelemetryService
{
    private static readonly ConcurrentQueue<JObject> Records = new ConcurrentQueue<JObject>();

    public const string TelemetryRelativePath = "ScanTelemetry_{timestamp}.json";

    private TelemetryMode telemetryMode = TelemetryMode.Production;

    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IFileWritingService FileWritingService { get; set; }

    public void Flush() => this.FileWritingService.WriteFile(TelemetryRelativePath, JsonConvert.SerializeObject(Records));

    public void PostRecord(IDetectionTelemetryRecord record)
    {
        if (this.telemetryMode != TelemetryMode.Disabled)
        {
            var jsonRecord = JObject.FromObject(record);
            jsonRecord.Add("Timestamp", DateTime.UtcNow);
            jsonRecord.Add("CorrelationId", TelemetryConstants.CorrelationId);

            Records.Enqueue(jsonRecord);

            if (this.telemetryMode == TelemetryMode.Debug)
            {
                this.Logger.LogInfo(jsonRecord.ToString());
            }
        }
    }

    public void SetMode(TelemetryMode mode) => this.telemetryMode = mode;
}

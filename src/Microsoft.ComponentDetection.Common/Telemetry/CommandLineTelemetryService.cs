namespace Microsoft.ComponentDetection.Common.Telemetry;
using System;
using System.Collections.Concurrent;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal class CommandLineTelemetryService : ITelemetryService
{
    private static readonly ConcurrentQueue<JObject> Records = new ConcurrentQueue<JObject>();

    public const string TelemetryRelativePath = "ScanTelemetry_{timestamp}.json";

    private readonly IFileWritingService fileWritingService;
    private readonly ILogger logger;

    private TelemetryMode telemetryMode = TelemetryMode.Production;

    public CommandLineTelemetryService(ILogger logger, IFileWritingService fileWritingService)
    {
        this.logger = logger;
        this.fileWritingService = fileWritingService;
    }

    public void Flush()
    {
        this.fileWritingService.WriteFile(TelemetryRelativePath, JsonConvert.SerializeObject(Records));
    }

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
                this.logger.LogInfo(jsonRecord.ToString());
            }
        }
    }

    public void SetMode(TelemetryMode mode)
    {
        this.telemetryMode = mode;
    }
}

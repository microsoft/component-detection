#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry;

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.Extensions.Logging;

/// <summary>
/// A telemetry service that writes records to a file.
/// </summary>
internal class CommandLineTelemetryService : ITelemetryService
{
    private const string TelemetryRelativePath = "ScanTelemetry_{timestamp}.json";
    private readonly ConcurrentQueue<JsonNode> records = new();
    private readonly IFileWritingService fileWritingService;
    private readonly ILogger logger;
    private TelemetryMode telemetryMode = TelemetryMode.Production;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineTelemetryService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="fileWritingService">The file writing service.</param>
    public CommandLineTelemetryService(ILogger<CommandLineTelemetryService> logger, IFileWritingService fileWritingService)
    {
        this.logger = logger;
        this.fileWritingService = fileWritingService;
    }

    /// <inheritdoc/>
    public void Flush() => this.fileWritingService.WriteFile(TelemetryRelativePath, JsonSerializer.Serialize(this.records));

    /// <inheritdoc/>
    public void PostRecord(IDetectionTelemetryRecord record)
    {
        if (this.telemetryMode == TelemetryMode.Disabled)
        {
            return;
        }

        var jsonRecord = JsonSerializer.SerializeToNode(record, record.GetType());
        jsonRecord["Timestamp"] = DateTime.UtcNow;
        jsonRecord["CorrelationId"] = TelemetryConstants.CorrelationId;

        this.records.Enqueue(jsonRecord);

        if (this.telemetryMode == TelemetryMode.Debug)
        {
            this.logger.LogInformation("Telemetry record: {Record}", jsonRecord.ToString());
        }
    }

    /// <inheritdoc/>
    public void SetMode(TelemetryMode mode) => this.telemetryMode = mode;
}

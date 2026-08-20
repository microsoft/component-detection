#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry;

using System;
using System.Collections.Concurrent;
using System.Linq;
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

        // Mask sensitive information in all string values before storing/logging
        MaskSensitiveInformation(jsonRecord);

        this.records.Enqueue(jsonRecord);

        if (this.telemetryMode == TelemetryMode.Debug)
        {
            this.logger.LogInformation("Telemetry record: {Record}", jsonRecord.ToString());
        }
    }

    /// <inheritdoc/>
    public void SetMode(TelemetryMode mode) => this.telemetryMode = mode;

    /// <summary>
    /// Recursively masks sensitive information in all string values within a JSON node.
    /// </summary>
    /// <param name="node">The JSON node to process.</param>
    private static void MaskSensitiveInformation(JsonNode node)
    {
        if (node == null)
        {
            return;
        }

        if (node is JsonObject jsonObject)
        {
            // Get keys first to avoid collection modified during enumeration
            var keys = jsonObject.Select(p => p.Key).ToList();
            foreach (var key in keys)
            {
                var value = jsonObject[key];
                if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
                {
                    // Mask sensitive info in string values
                    jsonObject[key] = stringValue.RemoveSensitiveInformation();
                }
                else
                {
                    // Recurse into nested objects and arrays
                    MaskSensitiveInformation(value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                var item = jsonArray[index];
                if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
                {
                    jsonArray[index] = stringValue.RemoveSensitiveInformation();
                }
                else
                {
                    MaskSensitiveInformation(item);
                }
            }
        }
    }
}

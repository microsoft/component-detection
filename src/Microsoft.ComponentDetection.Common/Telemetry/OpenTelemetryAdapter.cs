namespace Microsoft.ComponentDetection.Common.Telemetry;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.ComponentDetection.Common.Telemetry.Attributes;
using Microsoft.ComponentDetection.Common.Telemetry.Records;

/// <summary>
/// Adapter that emits telemetry records to both legacy telemetry services and OpenTelemetry.
/// Enables gradual migration to OTel while maintaining backward compatibility.
/// </summary>
public class OpenTelemetryAdapter : ITelemetryService
{
    private readonly ITelemetryService legacyTelemetryService;
    private readonly bool isEnabled;

    private readonly Counter<long>? detectorExecutionCounter;
    private readonly Histogram<double>? detectorExecutionDuration;
    private readonly Counter<long>? componentsDetectedCounter;
    private readonly Counter<long>? alertsCreatedCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryAdapter"/> class.
    /// </summary>
    /// <param name="legacyTelemetryService">The legacy telemetry service to forward records to.</param>
    /// <param name="enableOpenTelemetry">Whether OpenTelemetry is enabled (controlled by feature flag).</param>
    public OpenTelemetryAdapter(ITelemetryService legacyTelemetryService, bool enableOpenTelemetry = false)
    {
        this.legacyTelemetryService = legacyTelemetryService;
        this.isEnabled = enableOpenTelemetry;

        // Initialize meters only if OTel is enabled to avoid overhead
        if (this.isEnabled)
        {
            this.detectorExecutionCounter = SemanticConventions.Meter.CreateCounter<long>(
                SemanticConventions.Metrics.DetectorExecutionCount,
                description: "Number of detector executions");

            this.detectorExecutionDuration = SemanticConventions.Meter.CreateHistogram<double>(
                SemanticConventions.Metrics.DetectorExecutionDuration,
                unit: "ms",
                description: "Duration of detector execution in milliseconds");

            this.componentsDetectedCounter = SemanticConventions.Meter.CreateCounter<long>(
                SemanticConventions.Metrics.ComponentsDetected,
                description: "Number of components detected");

            this.alertsCreatedCounter = SemanticConventions.Meter.CreateCounter<long>(
                SemanticConventions.Metrics.AlertsCreated,
                description: "Number of alerts created");
        }
    }

    /// <inheritdoc/>
    public void PostRecord(IDetectionTelemetryRecord record)
    {
        // Always post to legacy telemetry service for backward compatibility
        this.legacyTelemetryService.PostRecord(record);

        // Only emit to OTel if enabled (feature flag)
        if (this.isEnabled)
        {
            this.EmitToOpenTelemetry(record);
        }
    }

    /// <inheritdoc/>
    public void Flush()
    {
        this.legacyTelemetryService.Flush();

        // OTel exporters handle flushing automatically via SDK lifecycle
    }

    /// <inheritdoc/>
    public void SetMode(TelemetryMode mode)
    {
        this.legacyTelemetryService.SetMode(mode);

        // OTel mode is controlled by feature flag, not TelemetryMode
    }

    /// <summary>
    /// Emits telemetry record to OpenTelemetry as traces and metrics.
    /// </summary>
    /// <param name="record">The telemetry record to emit.</param>
    private void EmitToOpenTelemetry(IDetectionTelemetryRecord record)
    {
        // Create activity (span) for the telemetry record
        using var activity = SemanticConventions.ActivitySource.StartActivity(
            record.RecordName,
            ActivityKind.Internal);

        if (activity == null)
        {
            // Activity sampling decided not to create this activity
            return;
        }

        // Extract properties via reflection to map to OTel attributes
        var properties = record.GetType().GetProperties();
        var tags = new List<KeyValuePair<string, object>>();

        foreach (var property in properties)
        {
            var value = property.GetValue(record);
            if (value == null || property.Name == nameof(IDetectionTelemetryRecord.RecordName))
            {
                continue;
            }

            // Check if this is a metric attribute
            var isMetric = property.CustomAttributes.Any(x => x.AttributeType == typeof(MetricAttribute));

            // Map property names to semantic convention attributes
            var attributeName = this.MapPropertyToAttribute(property.Name);

            if (isMetric)
            {
                // Record metric values
                this.RecordMetric(property.Name, value);
            }

            // Add all properties as tags/attributes (convert to string for non-primitive types)
            var tagValue = this.ConvertToTagValue(value);
            if (tagValue != null)
            {
                tags.Add(new KeyValuePair<string, object>(attributeName, tagValue));
            }
        }

        // Add all tags to the activity
        foreach (var tag in tags)
        {
            activity.SetTag(tag.Key, tag.Value);
        }

        // Set activity status based on success/failure indicators
        this.SetActivityStatus(activity, record);
    }

    /// <summary>
    /// Maps property name to OpenTelemetry semantic convention attribute name.
    /// </summary>
    /// <param name="propertyName">The property name from the telemetry record.</param>
    /// <returns>The semantic convention attribute name.</returns>
    private string MapPropertyToAttribute(string propertyName)
    {
        // Map common property names to semantic conventions
        return propertyName switch
        {
            "Command" => SemanticConventions.Attributes.CodeFunction,
            "ExitCode" => SemanticConventions.Attributes.ProcessExitCode,
            "DetectorId" => SemanticConventions.ComponentDetection.DetectorId,
            "DetectorCategory" => SemanticConventions.ComponentDetection.DetectorCategory,
            "ComponentsCount" or "DetectedComponentCount" => SemanticConventions.ComponentDetection.ComponentCount,
            "ScanType" => SemanticConventions.ComponentDetection.ScanType,
            "SourceDirectory" => SemanticConventions.ComponentDetection.SourceDirectory,
            "Timeout" => SemanticConventions.ComponentDetection.Timeout,
            "ErrorMessage" => SemanticConventions.Attributes.ErrorMessage,
            "ExceptionMessage" => SemanticConventions.Attributes.ErrorMessage,

            // Azure DevOps specific mappings
            "BuildProjectId" => SemanticConventions.AzureDevOps.BuildProjectId,
            "RepoProjectId" or "RepositoryProjectId" => SemanticConventions.AzureDevOps.RepositoryProjectId,
            "PipelineId" => SemanticConventions.AzureDevOps.PipelineId,
            "PhaseId" => SemanticConventions.AzureDevOps.PhaseId,
            "PhaseDisplayName" => SemanticConventions.AzureDevOps.PhaseDisplayName,
            "BuildId" => SemanticConventions.AzureDevOps.BuildId,
            "BuildNumber" => SemanticConventions.AzureDevOps.BuildNumber,
            "BuildUri" => SemanticConventions.AzureDevOps.BuildUri,
            "OrganizationId" => SemanticConventions.AzureDevOps.OrganizationId,
            "CollectionId" => SemanticConventions.AzureDevOps.CollectionId,
            "AlertSeverity" => SemanticConventions.AzureDevOps.AlertSeverity,
            "AlertWarningLevel" => SemanticConventions.AzureDevOps.AlertWarningLevel,
            "AutoInjected" => SemanticConventions.AzureDevOps.AutoInjected,
            "TaskVersion" => SemanticConventions.AzureDevOps.TaskVersion,
            "GovernanceUrl" or "VstsUrl" => SemanticConventions.AzureDevOps.GovernanceUrl,
            "PullRequestId" => SemanticConventions.AzureDevOps.PullRequestId,
            "CallerIdentifier" => SemanticConventions.AzureDevOps.CallerIdentifier,
            "HiddenExitCode" => SemanticConventions.AzureDevOps.DetectionHiddenExitCode,
            "BuildTaskInput" => SemanticConventions.AzureDevOps.TaskInput,
            "Arguments" => SemanticConventions.AzureDevOps.DetectionArguments,
            "AgentOSMeaningfulDetails" => SemanticConventions.AzureDevOps.AgentOsMeaningfulDetails,
            "AgentOSDescription" => SemanticConventions.AzureDevOps.AgentOsDescription,
            "DetectionVersion" => SemanticConventions.AzureDevOps.DetectionVersion,
            "RunSource" => SemanticConventions.AzureDevOps.RunSource,
            "UnhandledException" => SemanticConventions.Attributes.ErrorStackTrace,

            // Default: use property name as-is but convert to lowercase with dots
            _ => this.ConvertToSnakeCase(propertyName),
        };
    }

    /// <summary>
    /// Records metric values to OpenTelemetry meters.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The metric value.</param>
    private void RecordMetric(string propertyName, object value)
    {
        // Convert value to appropriate numeric type
        var numericValue = this.ConvertToNumeric(value);
        if (!numericValue.HasValue)
        {
            return;
        }

        // Record to appropriate metric instrument based on property name
        switch (propertyName)
        {
            case "DetectedComponentCount" or "ComponentsCount":
                this.componentsDetectedCounter?.Add((long)numericValue.Value);
                break;

            case "ExecutionTime" or "ElapsedMilliseconds":
                this.detectorExecutionDuration?.Record(numericValue.Value);
                break;

            case "AlertsCount" or "CriticalAlertsCount" or "HighAlertsCount":
                this.alertsCreatedCounter?.Add((long)numericValue.Value);
                break;

            default:
                // Property doesn't map to a known metric
                break;
        }
    }

    /// <summary>
    /// Sets the activity status based on record properties.
    /// </summary>
    /// <param name="activity">The activity to set status on.</param>
    /// <param name="record">The telemetry record.</param>
    private void SetActivityStatus(Activity activity, IDetectionTelemetryRecord record)
    {
        // Check for error indicators in the record
        var properties = record.GetType().GetProperties();
        var hasError = false;
        string? errorMessage = null;

        foreach (var property in properties)
        {
            var value = property.GetValue(record);
            if (value == null)
            {
                continue;
            }

            // Check for non-zero exit codes
            if (property.Name == "ExitCode" && value is int exitCode && exitCode != 0)
            {
                hasError = true;
                errorMessage = $"Process exited with code {exitCode}";
                break;
            }

            // Check for error-related properties
            if (property.Name.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Failed", StringComparison.OrdinalIgnoreCase))
            {
                hasError = true;
                errorMessage = value.ToString();
                break;
            }
        }

        if (hasError)
        {
            activity.SetStatus(ActivityStatusCode.Error, errorMessage);
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }

    /// <summary>
    /// Converts property name to snake_case for attribute naming.
    /// </summary>
    /// <param name="input">The input string in PascalCase.</param>
    /// <returns>The string in snake_case.</returns>
    private string ConvertToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var result = new List<char>();
        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            if (char.IsUpper(current) && i > 0)
            {
                result.Add('.');
                result.Add(char.ToLowerInvariant(current));
            }
            else
            {
                result.Add(char.ToLowerInvariant(current));
            }
        }

        return new string(result.ToArray());
    }

    /// <summary>
    /// Converts value to a tag value compatible with OTel (primitives and strings).
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value, or null if not convertible.</returns>
    private object? ConvertToTagValue(object value)
    {
        return value switch
        {
            string s => s,
            int i => i,
            long l => l,
            double d => d,
            float f => f,
            bool b => b,
            Enum e => e.ToString(),
            TimeSpan ts => ts.TotalMilliseconds,
            _ => value.ToString(),
        };
    }

    /// <summary>
    /// Converts value to numeric for metrics.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The numeric value, or null if not convertible.</returns>
    private double? ConvertToNumeric(object value)
    {
        return value switch
        {
            int i => i,
            long l => l,
            double d => d,
            float f => f,
            TimeSpan ts => ts.TotalMilliseconds,
            _ => null,
        };
    }
}

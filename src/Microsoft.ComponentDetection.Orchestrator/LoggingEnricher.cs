namespace Microsoft.ComponentDetection.Orchestrator;

using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Enriches log events with the log file path, derived from the command line arguments.
/// </summary>
public class LoggingEnricher : ILogEventEnricher
{
    /// <summary>
    /// The name of the log file path property.
    /// </summary>
    public const string LogFilePathPropertyName = "LogFilePath";
    private string cachedLogFilePath;
    private LogEventProperty cachedLogFilePathProperty;

    /// <summary>
    /// The name of the print stderr property.
    /// </summary>
    public const string PrintStderrPropertyName = "PrintStderr";
    private bool? cachedPrintStderr;
    private LogEventProperty cachedPrintStderrProperty;

    /// <summary>
    /// The path to the log file.
    /// </summary>
    public static string Path { get; set; } = string.Empty;

    /// <summary>
    /// <c>true</c> if logs should be printed to stderr; otherwise, <c>false</c>.
    /// </summary>
    public static bool PrintStderr { get; set; }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        InvalidateLogEventProperty(
            logEvent,
            propertyFactory,
            LogFilePathPropertyName,
            Path,
            ref this.cachedLogFilePathProperty,
            ref this.cachedLogFilePath);

        InvalidateLogEventProperty(
            logEvent,
            propertyFactory,
            PrintStderrPropertyName,
            PrintStderr,
            ref this.cachedPrintStderrProperty,
            ref this.cachedPrintStderr);
    }

    private static void InvalidateLogEventProperty<T>(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory,
        string propertyName,
        T propertyValue,
        ref LogEventProperty cachedLogEventProperty,
        ref T cachedPropertyValue)
    {
        // the settings might not have a value or we might not be within a command in which case
        // we won't have the setting so a default value for will be required
        LogEventProperty property;

        if (cachedPropertyValue != null && propertyValue.Equals(cachedPropertyValue))
        {
            // hasn't changed, so let's use the cached property
            property = cachedLogEventProperty;
        }
        else
        {
            // We've got a new value. Let's create a new property and cache it for future log events to use
            cachedPropertyValue = propertyValue;
            cachedLogEventProperty = property = propertyFactory.CreateProperty(propertyName, propertyValue);
        }

        logEvent.AddPropertyIfAbsent(property);
    }
}

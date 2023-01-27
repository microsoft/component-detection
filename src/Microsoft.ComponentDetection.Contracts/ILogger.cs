namespace Microsoft.ComponentDetection.Contracts;
using System;
using System.Runtime.CompilerServices;

/// <summary>Simple abstraction around console/output file logging for component detection.</summary>
public interface ILogger
{
    /// <summary>Creates a logical separation (e.g. newline) between different log messages.</summary>
    void LogCreateLoggingGroup();

    /// <summary>Logs a warning message, outputting if configured verbosity is higher than Quiet.</summary>
    /// <param name="message">The message to output.</param>
    void LogWarning(string message);

    /// <summary>Logs an informational message, outputting if configured verbosity is higher than Quiet.</summary>
    /// <param name="message">The message to output.</param>
    void LogInfo(string message);

    /// <summary>Logs a verbose message, outputting if configured verbosity is at least Verbose.</summary>
    /// <param name="message">The message to output.</param>
    void LogVerbose(string message);

    /// <summary>Logs an error message, outputting for all verbosity levels.</summary>
    /// <param name="message">The message to output.</param>
    void LogError(string message);

    /// <summary>Logs a specially formatted message if a file read failed, outputting if configured verbosity is at least Verbose.</summary>
    /// <param name="filePath">The file path responsible for the file reading failure.</param>
    /// <param name="e">The exception encountered when reading a file.</param>
    void LogFailedReadingFile(string filePath, Exception e);

    /// <summary>Logs a specially formatted message if an exception has occurred.</summary>
    /// <param name="e">The exception to log the occurance of.</param>
    /// <param name="isError">Whether or not the exception represents a true error case (e.g. unexpected) vs. expected.</param>
    /// <param name="printException">Indicate if the exception is going to be fully printed.</param>
    /// <param name="callerMemberName">Implicity populated arg, provides the member name of the calling function to the log message.</param>
    /// <param name="callerLineNumber">Implicitly populated arg, provides calling line number.</param>
    void LogException(
        Exception e,
        bool isError,
        bool printException = false,
        [CallerMemberName] string callerMemberName = "",
        [CallerLineNumber] int callerLineNumber = 0);

    /// <summary>
    /// Log a warning to the build console, adding it to the build summary and turning the build yellow.
    /// </summary>
    /// <param name="message">The message to display alongside the warning.</param>
    void LogBuildWarning(string message);

    /// <summary>
    /// Log an error to the build console, adding it to the build summary and turning the build red.
    /// </summary>
    /// <param name="message">The message to display alongside the warning.</param>
    void LogBuildError(string message);
}

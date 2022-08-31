using System;
using System.Composition;
using System.Runtime.CompilerServices;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;

using static System.Environment;

namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(ILogger))]
    [Export(typeof(Logger))]
    [Shared]
    public class Logger : ILogger
    {
        [Import]
        public IFileWritingService FileWritingService { get; set; }

        [Import]
        public IConsoleWritingService ConsoleWriter { get; set; }

        private VerbosityMode Verbosity { get; set; }

        private bool WriteToFile { get; set; }

        public const string LogRelativePath = "GovCompDisc_Log_{timestamp}.log";

        public void Init(VerbosityMode verbosity)
        {
            this.WriteToFile = true;
            this.Verbosity = verbosity;
            try
            {
                this.FileWritingService.WriteFile(LogRelativePath, string.Empty);
                this.LogInfo($"Log file: {this.FileWritingService.ResolveFilePath(LogRelativePath)}");
            }
            catch (Exception)
            {
                this.WriteToFile = false;
                this.LogError("There was an issue writing to the log file, for the remainder of execution verbose output will be written to the console.");
                this.Verbosity = VerbosityMode.Verbose;
            }
        }

        public void LogCreateLoggingGroup()
        {
            this.PrintToConsole(NewLine, VerbosityMode.Normal);
            this.AppendToFile(NewLine);
        }

        public void LogWarning(string message)
        {
            this.LogInternal("WARN", message);
        }

        public void LogInfo(string message)
        {
            this.LogInternal("INFO", message);
        }

        public void LogVerbose(string message)
        {
            this.LogInternal("VERBOSE", message, VerbosityMode.Verbose);
        }

        public void LogError(string message)
        {
            this.LogInternal("ERROR", message, VerbosityMode.Quiet);
        }

        public void LogFailedReadingFile(string filePath, Exception e)
        {
            this.PrintToConsole(NewLine, VerbosityMode.Verbose);
            this.LogFailedProcessingFile(filePath);
            this.LogException(e, isError: false);
            using var record = new FailedReadingFileRecord
            {
                FilePath = filePath,
                ExceptionMessage = e.Message,
                StackTrace = e.StackTrace,
            };
        }

        public void LogException(
            Exception e,
            bool isError,
            bool printException = false,
            [CallerMemberName] string callerMemberName = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            var tag = isError ? "[ERROR]" : "[INFO]";

            var fullExceptionText = $"{tag} Exception encountered." + NewLine +
                $"CallerMember: [{callerMemberName} : {callerLineNumber}]" + NewLine +
                e.ToString() + NewLine;

            var shortExceptionText = $"{tag} {callerMemberName} logged {e.GetType().Name}: {e.Message}{NewLine}";

            var consoleText = printException ? fullExceptionText : shortExceptionText;

            if (isError)
            {
                this.PrintToConsole(consoleText, VerbosityMode.Quiet);
            }
            else
            {
                this.PrintToConsole(consoleText, VerbosityMode.Verbose);
            }

            this.AppendToFile(fullExceptionText);
        }

        // TODO: All these vso specific logs should go away
        public void LogBuildWarning(string message)
        {
            this.PrintToConsole($"##vso[task.LogIssue type=warning;]{message}{NewLine}", VerbosityMode.Quiet);
        }

        public void LogBuildError(string message)
        {
            this.PrintToConsole($"##vso[task.LogIssue type=error;]{message}{NewLine}", VerbosityMode.Quiet);
        }

        private void LogFailedProcessingFile(string filePath)
        {
            this.LogVerbose($"Could not read component details from file {filePath} {NewLine}");
        }

        private void AppendToFile(string text)
        {
            if (this.WriteToFile)
            {
                this.FileWritingService.AppendToFile(LogRelativePath, text);
            }
        }

        private void PrintToConsole(string text, VerbosityMode minVerbosity)
        {
            if (this.Verbosity >= minVerbosity)
            {
                this.ConsoleWriter.Write(text);
            }
        }

        private void LogInternal(string prefix, string message, VerbosityMode verbosity = VerbosityMode.Normal)
        {
            var formattedPrefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : $"[{prefix}] ";
            var text = $"{formattedPrefix}{message} {NewLine}";

            this.PrintToConsole(text, verbosity);
            this.AppendToFile(text);
        }
    }
}

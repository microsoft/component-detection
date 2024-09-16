namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;
using Microsoft.ComponentDetection.Contracts;

public class CommandLineInvocationTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "CommandLineInvocation";

    public string PathThatWasRan { get; set; }

    public string Parameters { get; set; }

    public int? ExitCode { get; set; }

    public string StandardError { get; set; }

    public string UnhandledException { get; set; }

    internal void Track(CommandLineExecutionResult result, string path, string parameters)
    {
        this.ExitCode = result.ExitCode;
        this.StandardError = result.StdErr;
        this.TrackCommon(path, parameters);
    }

    internal void Track(Exception ex, string path, string parameters)
    {
        this.ExitCode = -1;
        this.UnhandledException = ex.ToString();
        this.TrackCommon(path, parameters);
    }

    private void TrackCommon(string path, string parameters)
    {
        this.PathThatWasRan = path;
        this.Parameters = parameters;
        this.StopExecutionTimer();
    }
}

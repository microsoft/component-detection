namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System;
using System.ComponentModel;
using Microsoft.ComponentDetection.Contracts;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Base settings for all commands.
/// </summary>
public abstract class BaseSettings : CommandSettings
{
    [Description("Wait for debugger on start")]
    [CommandOption("--Debug")]
    public bool Debug { get; init; }

    [Description("Used to output all telemetry events to the console.")]
    [CommandOption("--DebugTelemetry")]
    public bool DebugTelemetry { get; set; }

    [Description("Identifier used to correlate all telemetry for a given execution. If not provided, a new GUID will be generated.")]
    [CommandOption("--CorrelationId")]
    public Guid CorrelationId { get; set; }

    [Description("Flag indicating what level of logging to output to console during execution. Options are: Verbose, Normal, or Quiet.")]
    [DefaultValue(VerbosityMode.Normal)]
    [CommandOption("--Verbosity")]
    public VerbosityMode Verbosity { get; set; }

    [Description("An integer representing the time limit (in seconds) before detection is cancelled")]
    [CommandOption("--Timeout")]
    public int? Timeout { get; set; }

    [Description("Output path for log files. Defaults to %TMP%")]
    [CommandOption("--Output")]
    public string Output { get; set; }

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        if (this.Timeout is <= 0)
        {
            return ValidationResult.Error("Timeout must be a positive integer");
        }

        return base.Validate();
    }
}

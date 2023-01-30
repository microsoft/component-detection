namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Microsoft.ComponentDetection.Common;
using Newtonsoft.Json;

public class BaseArguments : IScanArguments
{
    [Option("Debug", Required = false, HelpText = "Wait for debugger on start")]
    public bool Debug { get; set; }

    [Option("DebugTelemetry", Required = false, HelpText = "Used to output all telemetry events to the console.")]
    public bool DebugTelemetry { get; set; }

    [JsonIgnore]
    [Option("AdditionalPluginDirectories", Separator = ';', Required = false, Hidden = true, HelpText = "Semi-colon delimited list of directories to search for plugins")]
    public IEnumerable<DirectoryInfo> AdditionalPluginDirectories { get; set; }

    public IEnumerable<string> AdditionalPluginDirectoriesSerialized => this.AdditionalPluginDirectories?.Select(x => x.ToString()) ?? new List<string>();

    [Option("SkipPluginsDirectory", Required = false, Default = false, HelpText = "Skip searching of /Plugins directory for additional component detectors.")]
    public bool SkipPluginsDirectory { get; set; }

    [Option("CorrelationId", Required = false, HelpText = "Identifier used to correlate all telemetry for a given execution. If not provided, a new GUID will be generated.")]
    public Guid CorrelationId { get; set; }

    [Option("Verbosity", HelpText = "Flag indicating what level of logging to output to console during execution. Options are: Verbose, Normal, or Quiet.", Default = VerbosityMode.Normal)]
    public VerbosityMode Verbosity { get; set; }

    [Option("Timeout", Required = false, HelpText = "An integer representing the time limit (in seconds) before detection is cancelled")]
    public int Timeout { get; set; }

    [Option("Output", Required = false, HelpText = "Output path for log files. Defaults to %TMP%")]
    public string Output { get; set; }

    [Option("AdditionalDITargets", Required = false, Separator = ',', HelpText = "Comma separated list of paths to additional dependency injection targets.")]
    public IEnumerable<string> AdditionalDITargets { get; set; }
}

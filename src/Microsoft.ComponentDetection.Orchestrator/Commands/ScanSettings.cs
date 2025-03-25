namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Settings for the Scan command.
/// </summary>
public class ScanSettings : BaseSettings
{
    [CommandOption("--DirectoryExclusionList")]
    [Description("Filters out specific directories following semi-colon separated glob patterns.")]
    [TypeConverter(typeof(SemicolonDelimitedConverter))]
    public IEnumerable<string> DirectoryExclusionList { get; set; }

    [CommandOption("--IgnoreDirectories", IsHidden = true)]
    [Description(
        "Filters out specific directories, providing individual directory paths separated by commas. Obsolete in favor of DirectoryExclusionList")]
    [TypeConverter(typeof(CommaDelimitedConverter))]
    public IEnumerable<string> DirectoryExclusionListObsolete { get; set; }

    [CommandOption("--SourceDirectory")]
    [Description("Directory to operate on.")]
    [JsonIgnore]
    public DirectoryInfo SourceDirectory { get; set; }

    public string SourceDirectorySerialized => this.SourceDirectory?.ToString();

    [CommandOption("--SourceFileRoot")]
    [Description("Directory where source files can be found.")]
    [JsonIgnore]
    public DirectoryInfo SourceFileRoot { get; set; }

    public string SourceFileRootSerialized => this.SourceFileRoot?.ToString();

    [CommandOption("--DetectorArgs")]
    [Description(
        "Comma separated list of properties that can affect the detectors execution, like EnableIfDefaultOff that allows a specific detector that is in beta to run, the format for this property is DetectorId=EnableIfDefaultOff, for example Pip=EnableIfDefaultOff.")]
    [TypeConverter(typeof(KeyValueDelimitedConverter))]
    public IDictionary<string, string> DetectorArgs { get; set; } = new Dictionary<string, string>();

    [CommandOption("--DetectorCategories")]
    [Description(
        "A comma separated list with the categories of components that are going to be scanned. The detectors that are going to run are the ones that belongs to the categories. The possible values are: Npm, NuGet, Maven, RubyGems, Cargo, Pip, GoMod, CocoaPods, Linux.")]
    [TypeConverter(typeof(CommaDelimitedConverter))]
    public IEnumerable<string> DetectorCategories { get; set; }

    [CommandOption("--DetectorsFilter")]
    [Description(
        "A comma separated list with the identifiers of the specific detectors to be used. This is meant to be used for testing purposes only.")]
    [TypeConverter(typeof(CommaDelimitedConverter))]
    public IEnumerable<string> DetectorsFilter { get; set; }

    [CommandOption("--ManifestFile")]
    [Description("The file to write scan results to.")]
    [JsonIgnore]
    public FileInfo ManifestFile { get; set; }

    public string ManifestFileSerialized => this.ManifestFile?.ToString();

    [CommandOption("--PrintManifest")]
    [Description("Prints the manifest to standard output. Logging will be redirected to standard error.")]
    public bool PrintManifest { get; set; }

    [CommandOption("--DockerImagesToScan")]
    [Description(
        "Comma separated list of docker image names or hashes to execute container scanning on, ex: ubuntu:16.04, 56bab49eef2ef07505f6a1b0d5bd3a601dfc3c76ad4460f24c91d6fa298369ab")]
    [TypeConverter(typeof(CommaDelimitedConverter))]
    public IEnumerable<string> DockerImagesToScan { get; set; }

    [CommandOption("--NoSummary")]
    [Description("Do not display the detection summary on the standard output nor in the logs.")]
    public bool NoSummary { get; set; }

    [CommandOption("--MaxDetectionThreads")]
    [Description("Max number of parallel threads used for a single detection process, ex: PipReport, Npm, Nuget.")]
    public int? MaxDetectionThreads { get; set; }

    [CommandOption("--CleanupCreatedFiles")]
    [Description("Whether or not to cleanup files that are created during detection, based on the rules provided in each detector. Defaults to 'true'.")]
    public bool? CleanupCreatedFiles { get; set; }

    /// <inheritdoc />
    public override ValidationResult Validate()
    {
        if (this.SourceDirectory is null)
        {
            return ValidationResult.Error($"{nameof(this.SourceDirectory)} is required");
        }

        if (this.MaxDetectionThreads is <= 0)
        {
            return ValidationResult.Error($"{nameof(this.MaxDetectionThreads)} must be a positive integer");
        }

        return !this.SourceDirectory.Exists ? ValidationResult.Error($"The {nameof(this.SourceDirectory)} {this.SourceDirectory} does not exist") : base.Validate();
    }
}

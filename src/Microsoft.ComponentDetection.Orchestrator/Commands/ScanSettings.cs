namespace Microsoft.ComponentDetection.Orchestrator.Commands;

using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Spectre.Console.Cli;

/// <summary>
/// Settings for the Scan command.
/// </summary>
public class ScanSettings : BaseSettings
{
    [CommandOption("--DirectoryExclusionList")]
    [Description("Filters out specific directories following a minimatch pattern.")]
    [TypeConverter(typeof(SemicolonDelimitedConverter))]
    public IEnumerable<string> DirectoryExclusionList { get; set; }

    [CommandOption("--IgnoreDirectories", IsHidden = true)]
    [Description(
        "Filters out specific directories, providing individual directory paths separated by semicolon. Obsolete in favor of DirectoryExclusionList")]
    [TypeConverter(typeof(SemicolonDelimitedConverter))]
    public IEnumerable<string> DirectoryExclusionListObsolete { get; set; }

    [CommandOption("--SourceDirectory")]
    [Description("Directory to operate on.")]
    public DirectoryInfo SourceDirectory { get; set; }

    [CommandOption("--SourceFileRoot")]
    [Description("Directory where source files can be found.")]
    public DirectoryInfo SourceFileRoot { get; set; }

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
    public IEnumerable<string> DetectorsFilter { get; set; }

    [CommandOption("--ManifestFile")]
    [Description("The file to write scan results to.")]
    public FileInfo ManifestFile { get; set; }

    [CommandOption("--PrintManifest")]
    [Description("Prints the manifest to standard output. Logging will be redirected to standard error.")]
    public bool PrintManifest { get; set; }

    [CommandOption("--DockerImagesToScan")]
    [Description(
        "Comma separated list of docker image names or hashes to execute container scanning on, ex: ubuntu:16.04, 56bab49eef2ef07505f6a1b0d5bd3a601dfc3c76ad4460f24c91d6fa298369ab")]
    [TypeConverter(typeof(CommaDelimitedConverter))]
    public IEnumerable<string> DockerImagesToScan { get; set; }
}

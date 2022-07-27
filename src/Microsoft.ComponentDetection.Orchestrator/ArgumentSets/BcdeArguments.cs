using System.Collections.Generic;
using System.Composition;
using System.IO;
using CommandLine;
using Microsoft.ComponentDetection.Contracts.ArgumentSets;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets
{
    [Verb("scan", HelpText = "Scans components")]
    [Export(typeof(IScanArguments))]
    public class BcdeArguments : BaseArguments, IDetectionArguments
    {
        [Option("DirectoryExclusionList", Required = false, Separator = ';', HelpText = "Filters out specific directories following a minimatch pattern.")]
        public IEnumerable<string> DirectoryExclusionList { get; set; }

        [Option("IgnoreDirectories", Required = false, Separator = ',', HelpText = "Filters out specific directories, providing individual directory paths separated by semicolon. Obsolete in favor of DirectoryExclusionList's glob syntax.")]
        public IEnumerable<string> DirectoryExclusionListObsolete { get; set; }

        [JsonIgnore]
        [Option("SourceDirectory", Required = true, HelpText = "Directory to operate on.")]
        public DirectoryInfo SourceDirectory { get; set; }

        public string SourceDirectorySerialized => SourceDirectory?.ToString();

        [JsonIgnore]
        [Option("SourceFileRoot", Required = false, HelpText = "Directory where source files can be found.")]
        public DirectoryInfo SourceFileRoot { get; set; }

        public string SourceFileRootSerialized => SourceFileRoot?.ToString();

        [Option("DetectorArgs", Separator = ',', Required = false, HelpText = "Comma separated list of properties that can affect the detectors execution, like EnableIfDefaultOff that allows a specific detector that is in beta to run, the format for this property is " +
            "DetectorId=EnableIfDefaultOff, for example Pip=EnableIfDefaultOff.")]
        public IEnumerable<string> DetectorArgs { get; set; }

        [Option("DetectorCategories", Separator = ',', Required = false, HelpText = "A comma separated list with the categories of components that are going to be scanned. The detectors that are going to run are the ones that belongs to the categories." +
            "The possible values are: Npm, NuGet, Maven, RubyGems, Cargo, Pip, GoMod, CocoaPods, Linux.")]
        public IEnumerable<string> DetectorCategories { get; set; }

        [Option("DetectorsFilter", Separator = ',', Required = false, HelpText = "A comma separated list with the identifiers of the specific detectors to be used. This is meant to be used for testing purposes only.")]
        public IEnumerable<string> DetectorsFilter { get; set; }

        [Option("ManifestFileFormat", Required = false)]
        public ManifestFileFormat ManifestFileFormat { get; set; }

        [JsonIgnore]
        [Option("ManifestFile", Required = false, HelpText = "The file to write scan results to.")]
        public FileInfo ManifestFile { get; set; }

        public string ManifestFileSerialized => ManifestFile?.ToString();

        [Option("DockerImagesToScan", Required = false, Separator = ',', HelpText = "Comma separated list of docker image names or hashes to execute container scanning on, ex: ubuntu:16.04, 56bab49eef2ef07505f6a1b0d5bd3a601dfc3c76ad4460f24c91d6fa298369ab")]
        public IEnumerable<string> DockerImagesToScan { get; set; }
    }
}

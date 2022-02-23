using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts.ArgumentSets;

namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets
{
    public interface IDetectionArguments : IScanArguments
    {
        IEnumerable<string> DirectoryExclusionList { get; set; }

        IEnumerable<string> DirectoryExclusionListObsolete { get; set; }

        DirectoryInfo SourceDirectory { get; set; }

        DirectoryInfo SourceFileRoot { get; set; }

        IEnumerable<string> DetectorArgs { get; set; }

        IEnumerable<string> DetectorCategories { get; set; }

        IEnumerable<string> DetectorsFilter { get; set; }
        
        ManifestFileFormat ManifestFileFormat { get; set; }

        FileInfo ManifestFile { get; set; }

        IEnumerable<string> DockerImagesToScan { get; set; }
    }
}

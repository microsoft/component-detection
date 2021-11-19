namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    /// <summary>
    /// Take from https://github.com/anchore/syft/tree/main/schema/json.
    /// Match version to tag used i.e. https://github.com/anchore/syft/blob/v0.16.1/internal/constants.go#L9
    /// Can convert JSON Schema to C# using quicktype.io.
    /// </summary>
    public class SyftOutput
    {
        public Relationship[] ArtifactRelationships { get; set; }

        public Package[] Artifacts { get; set; }

        public Descriptor Descriptor { get; set; }

        public Distribution Distro { get; set; }

        public FileClassifications[] FileClassifications { get; set; }

        public FileContents[] FileContents { get; set; }

        public FileMetadata[] FileMetadata { get; set; }

        public Schema Schema { get; set; }

        public Secrets[] Secrets { get; set; }

        public Source Source { get; set; }
    }
}

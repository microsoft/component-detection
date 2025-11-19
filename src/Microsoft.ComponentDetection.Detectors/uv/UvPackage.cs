namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System.Collections.Generic;

    public class UvPackage
    {
        public required string Name { get; init; }

        public required string Version { get; init; }

        public List<UvDependency> Dependencies { get; set; } = [];

        // Metadata dependencies (requires-dist)
        public List<UvDependency> MetadataRequiresDist { get; set; } = [];

        // Metadata dev dependencies (requires-dev)
        public List<UvDependency> MetadataRequiresDev { get; set; } = [];

        // Source property for uv.lock
        public UvSource? Source { get; set; }
    }
}

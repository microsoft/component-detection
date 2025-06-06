namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System.Collections.Generic;

    public class UvPackage
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public List<UvDependency> Dependencies { get; set; } = [];

        // Metadata dependencies (requires-dist)
        public List<UvDependency> MetadataRequiresDist { get; set; } = [];

        // Metadata dev dependencies (requires-dev)
        public List<UvDependency> MetadataRequiresDev { get; set; } = [];
    }
}

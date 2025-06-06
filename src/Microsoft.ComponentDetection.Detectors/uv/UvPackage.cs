namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System.Collections.Generic;

    public class UvPackage
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public List<UvDependency> Dependencies { get; set; } = [];
    }
}

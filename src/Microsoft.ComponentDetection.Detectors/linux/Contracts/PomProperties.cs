using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class PomProperties
    {
        public string ArtifactId { get; set; }

        public Dictionary<string, string> ExtraFields { get; set; }

        public string GroupId { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public string Version { get; set; }
    }
}

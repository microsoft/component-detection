namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class Package
    {
        public string[] Cpes { get; set; }

        public string FoundBy { get; set; }

        public string Id { get; set; }

        public string Language { get; set; }

        public string[] Licenses { get; set; }

        public Location[] Locations { get; set; }

        public Metadata Metadata { get; set; }

        public string MetadataType { get; set; }

        public string Name { get; set; }

        public string Purl { get; set; }

        public string Type { get; set; }

        public string Version { get; set; }
    }
}

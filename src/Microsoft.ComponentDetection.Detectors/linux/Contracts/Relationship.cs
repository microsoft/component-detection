namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class Relationship
    {
        public string Child { get; set; }

        public ConfigurationUnion Metadata { get; set; }

        public string Parent { get; set; }

        public string Type { get; set; }
    }
}

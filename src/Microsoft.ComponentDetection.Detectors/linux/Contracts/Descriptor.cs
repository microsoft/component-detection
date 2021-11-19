namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class Descriptor
    {
        public ConfigurationUnion? Configuration { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }
    }
}

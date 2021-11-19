namespace Microsoft.ComponentDetection.Detectors.Yarn
{
    public class YarnDependency
    {
        public string LookupKey => $"{Name}@{Version}";

        public string Name { get; set; }

        public string Version { get; set; }
    }
}

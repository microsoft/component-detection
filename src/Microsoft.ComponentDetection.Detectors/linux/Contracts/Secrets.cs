namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class Secrets
    {
        public Location Location { get; set; }

        public SearchResult[] SecretsSecrets { get; set; }
    }
}

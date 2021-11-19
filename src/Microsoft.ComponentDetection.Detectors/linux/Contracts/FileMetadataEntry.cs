namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class FileMetadataEntry
    {
        public DigestElement[] Digests { get; set; }

        public long GroupId { get; set; }

        public string LinkDestination { get; set; }

        public long Mode { get; set; }

        public string Type { get; set; }

        public long UserId { get; set; }
    }
}

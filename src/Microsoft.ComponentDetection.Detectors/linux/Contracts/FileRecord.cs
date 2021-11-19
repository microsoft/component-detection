namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class FileRecord
    {
        public Digest Digest { get; set; }

        public string OwnerGid { get; set; }

        public string OwnerUid { get; set; }

        public string Path { get; set; }

        public string Permissions { get; set; }

        public bool? IsConfigFile { get; set; }

        public Size? Size { get; set; }

        public string Flags { get; set; }

        public string GroupName { get; set; }

        public long? Mode { get; set; }

        public string UserName { get; set; }
    }
}

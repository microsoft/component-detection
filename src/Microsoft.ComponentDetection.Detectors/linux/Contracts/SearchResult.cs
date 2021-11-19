namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class SearchResult
    {
        public string Classification { get; set; }

        public long Length { get; set; }

        public long LineNumber { get; set; }

        public long LineOffset { get; set; }

        public long SeekPosition { get; set; }

        public string Value { get; set; }
    }
}

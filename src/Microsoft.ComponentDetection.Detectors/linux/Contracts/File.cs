namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public struct File
    {
        public FileRecord FileRecord;
        public string String;

        public static implicit operator File(FileRecord fileRecord)
        {
            return new File { FileRecord = fileRecord };
        }

        public static implicit operator File(string @string)
        {
            return new File { String = @string };
        }
    }
}

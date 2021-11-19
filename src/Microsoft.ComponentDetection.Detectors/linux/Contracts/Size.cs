namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public struct Size
    {
        public long? Integer;
        public string String;

        public static implicit operator Size(long integer)
        {
            return new Size { Integer = integer };
        }

        public static implicit operator Size(string @string)
        {
            return new Size { String = @string };
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.Rust
{
    using System;
    using System.Runtime.Serialization;

    public class InvalidRustTomlFileException : Exception
    {
        public InvalidRustTomlFileException()
        {
        }

        public InvalidRustTomlFileException(string message)
            : base(message)
        {
        }

        public InvalidRustTomlFileException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected InvalidRustTomlFileException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}

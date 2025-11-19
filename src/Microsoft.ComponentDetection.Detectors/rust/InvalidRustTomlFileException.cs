#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;

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
}

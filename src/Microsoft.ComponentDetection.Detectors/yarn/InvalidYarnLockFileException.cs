#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System;

public class InvalidYarnLockFileException : Exception
{
    public InvalidYarnLockFileException()
    {
    }

    public InvalidYarnLockFileException(string message)
        : base(message)
    {
    }

    public InvalidYarnLockFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

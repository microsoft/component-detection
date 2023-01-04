using System;
using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Detectors.Yarn;

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

    protected InvalidYarnLockFileException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

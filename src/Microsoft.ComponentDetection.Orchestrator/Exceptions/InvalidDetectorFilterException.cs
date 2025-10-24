#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Exceptions;

using System;

[Serializable]
public class InvalidDetectorFilterException : Exception
{
    public InvalidDetectorFilterException()
    {
    }

    public InvalidDetectorFilterException(string message)
        : base(message)
    {
    }

    public InvalidDetectorFilterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

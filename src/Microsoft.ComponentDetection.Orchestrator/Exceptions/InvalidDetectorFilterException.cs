namespace Microsoft.ComponentDetection.Orchestrator.Exceptions;
using System;
using System.Runtime.Serialization;

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

    protected InvalidDetectorFilterException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

using System;
using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Orchestrator.Exceptions;

[Serializable]
internal class NoDetectorsFoundException : Exception
{
    public NoDetectorsFoundException()
        : base("Unable to load any detector plugins.")
    {
    }

    public NoDetectorsFoundException(string message)
        : base(message)
    {
    }

    public NoDetectorsFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected NoDetectorsFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

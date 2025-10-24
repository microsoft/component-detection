#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Exceptions;

using System;

[Serializable]
public class InvalidDetectorCategoriesException : Exception
{
    public InvalidDetectorCategoriesException()
    {
    }

    public InvalidDetectorCategoriesException(string message)
        : base(message)
    {
    }

    public InvalidDetectorCategoriesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

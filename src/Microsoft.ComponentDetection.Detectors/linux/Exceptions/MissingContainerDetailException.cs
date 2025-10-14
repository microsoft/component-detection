#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux.Exceptions;

using System;

public class MissingContainerDetailException : Exception
{
    public MissingContainerDetailException(string imageId)
        : base($"No container details information could be found for image ${imageId}")
    {
    }

    public MissingContainerDetailException()
    {
    }

    public MissingContainerDetailException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

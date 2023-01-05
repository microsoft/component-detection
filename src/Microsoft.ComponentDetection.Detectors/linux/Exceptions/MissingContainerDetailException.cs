using System;

namespace Microsoft.ComponentDetection.Detectors.Linux.Exceptions;

public class MissingContainerDetailException : Exception
{
    public MissingContainerDetailException(string imageId)
        : base($"No container details information could be found for image ${imageId}")
    {
    }
}

namespace Microsoft.ComponentDetection.Detectors.Linux.Exceptions
{
    using System;

    public class MissingContainerDetailException : Exception
    {
        public MissingContainerDetailException(string imageId)
            : base($"No container details information could be found for image ${imageId}")
        {
        }
    }
}

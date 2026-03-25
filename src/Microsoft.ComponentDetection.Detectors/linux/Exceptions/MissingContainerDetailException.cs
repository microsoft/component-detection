namespace Microsoft.ComponentDetection.Detectors.Linux.Exceptions;

using System;

/// <summary>
/// Exception thrown when container details information cannot be found for a specified image.
/// </summary>
public class MissingContainerDetailException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingContainerDetailException"/> class with the specified image ID.
    /// </summary>
    /// <param name="imageId">The ID of the container image for which details could not be found.</param>
    public MissingContainerDetailException(string imageId)
        : base($"No container details information could be found for image {imageId}")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingContainerDetailException"/> class.
    /// </summary>
    public MissingContainerDetailException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingContainerDetailException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public MissingContainerDetailException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace Microsoft.ComponentDetection.Common.Exceptions;

using System;

/// <summary>
/// Exception thrown when an unsupported version of CLI output is encountered.
/// </summary>
public class UnsupportedCliOutputVersionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedCliOutputVersionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnsupportedCliOutputVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedCliOutputVersionException"/> class.
    /// </summary>
    public UnsupportedCliOutputVersionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedCliOutputVersionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnsupportedCliOutputVersionException(string message)
        : base(message)
    {
    }
}

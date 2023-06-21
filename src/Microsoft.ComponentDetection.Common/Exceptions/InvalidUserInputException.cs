namespace Microsoft.ComponentDetection.Common.Exceptions;

using System;

/// <summary>
/// Exception thrown when the user input is invalid.
/// </summary>
public class InvalidUserInputException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidUserInputException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidUserInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidUserInputException"/> class.
    /// </summary>
    public InvalidUserInputException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidUserInputException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidUserInputException(string message)
        : base(message)
    {
    }
}

using System;

namespace Microsoft.ComponentDetection.Common.Exceptions;

public class InvalidUserInputException : Exception
{
    public InvalidUserInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

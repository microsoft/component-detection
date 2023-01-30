namespace Microsoft.ComponentDetection.Common.Exceptions;
using System;

public class InvalidUserInputException : Exception
{
    public InvalidUserInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;

public class PythonNotFoundException : Exception
{
    public PythonNotFoundException(string message)
        : base(message)
    {
    }

    public PythonNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PythonNotFoundException()
    {
    }
}

namespace Microsoft.ComponentDetection.Common;

using System;

internal class ConsoleWritingService : IConsoleWritingService
{
    public void Write(string content)
    {
        Console.Write(content);
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;

public class ConsoleWritingService : IConsoleWritingService
{
    public void Write(string content)
    {
        Console.Write(content);
    }
}

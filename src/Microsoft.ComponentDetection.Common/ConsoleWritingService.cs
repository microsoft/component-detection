using System;
using System.Composition;

namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(IConsoleWritingService))]
    public class ConsoleWritingService : IConsoleWritingService
    {
        public void Write(string content)
        {
            Console.Write(content);
        }
    }
}

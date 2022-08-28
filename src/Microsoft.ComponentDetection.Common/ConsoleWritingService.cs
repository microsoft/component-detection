namespace Microsoft.ComponentDetection.Common
{
    using System;
    using System.Composition;

    [Export(typeof(IConsoleWritingService))]
    public class ConsoleWritingService : IConsoleWritingService
    {
        public void Write(string content)
        {
            Console.Write(content);
        }
    }
}

using System.IO;

namespace Microsoft.ComponentDetection.Common
{
    // All file paths are relative and will replace occurrences of {timestamp} with the shared file timestamp.
    public interface IFileWritingService
    {
        void AppendToFile(string relativeFilePath, string text);

        void WriteFile(string relativeFilePath, string text);

        void WriteFile(FileInfo relativeFilePath, string text);

        string ResolveFilePath(string relativeFilePath);
    }
}

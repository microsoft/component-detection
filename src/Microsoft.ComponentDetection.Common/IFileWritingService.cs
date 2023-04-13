namespace Microsoft.ComponentDetection.Common;

using System;
using System.IO;
using System.Threading.Tasks;

// All file paths are relative and will replace occurrences of {timestamp} with the shared file timestamp.
public interface IFileWritingService : IDisposable, IAsyncDisposable
{
    void Init(string basePath);

    void AppendToFile(string relativeFilePath, string text);

    void WriteFile(string relativeFilePath, string text);

    Task WriteFileAsync(string relativeFilePath, string text);

    void WriteFile(FileInfo relativeFilePath, string text);

    string ResolveFilePath(string relativeFilePath);
}

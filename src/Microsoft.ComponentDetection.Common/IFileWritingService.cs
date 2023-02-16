﻿namespace Microsoft.ComponentDetection.Common;

using System;
using System.IO;

// All file paths are relative and will replace occurrences of {timestamp} with the shared file timestamp.
public interface IFileWritingService : IDisposable
{
    void Init(string basePath);

    void AppendToFile(string relativeFilePath, string text);

    void WriteFile(string relativeFilePath, string text);

    void WriteFile(FileInfo relativeFilePath, string text);

    string ResolveFilePath(string relativeFilePath);
}

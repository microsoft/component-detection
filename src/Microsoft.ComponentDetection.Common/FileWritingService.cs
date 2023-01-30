namespace Microsoft.ComponentDetection.Common;
using System;
using System.Composition;
using System.IO;
using Microsoft.ComponentDetection.Common.Exceptions;

[Export(typeof(IFileWritingService))]
[Export(typeof(FileWritingService))]
[Shared]
public class FileWritingService : IFileWritingService
{
    public const string TimestampFormatString = "yyyyMMddHHmmssfff";

    private readonly object lockObject = new object();
    private readonly string timestamp = DateTime.Now.ToString(TimestampFormatString);

    public string BasePath { get; private set; }

    public void Init(string basePath)
    {
        if (!string.IsNullOrEmpty(basePath) && !Directory.Exists(basePath))
        {
            throw new InvalidUserInputException($"The path {basePath} does not exist.", new DirectoryNotFoundException());
        }

        this.BasePath = string.IsNullOrEmpty(basePath) ? Path.GetTempPath() : basePath;
    }

    public void AppendToFile(string relativeFilePath, string text)
    {
        relativeFilePath = this.ResolveFilePath(relativeFilePath);

        lock (this.lockObject)
        {
            File.AppendAllText(relativeFilePath, text);
        }
    }

    public void WriteFile(string relativeFilePath, string text)
    {
        relativeFilePath = this.ResolveFilePath(relativeFilePath);

        lock (this.lockObject)
        {
            File.WriteAllText(relativeFilePath, text);
        }
    }

    public void WriteFile(FileInfo absolutePath, string text)
    {
        File.WriteAllText(absolutePath.FullName, text);
    }

    public string ResolveFilePath(string relativeFilePath)
    {
        this.EnsureInit();
        if (relativeFilePath.Contains("{timestamp}"))
        {
            relativeFilePath = relativeFilePath.Replace("{timestamp}", this.timestamp);
        }

        relativeFilePath = Path.Combine(this.BasePath, relativeFilePath);
        return relativeFilePath;
    }

    private void EnsureInit()
    {
        if (string.IsNullOrEmpty(this.BasePath))
        {
            throw new InvalidOperationException("Base path has not yet been initialized in File Writing Service!");
        }
    }
}

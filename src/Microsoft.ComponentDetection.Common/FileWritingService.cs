namespace Microsoft.ComponentDetection.Common;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Exceptions;

public sealed class FileWritingService : IFileWritingService
{
    public const string TimestampFormatString = "yyyyMMddHHmmssfff";

    private readonly object lockObject = new object();
    private readonly string timestamp = DateTime.Now.ToString(TimestampFormatString);
    private readonly ConcurrentDictionary<string, StreamWriter> bufferedStreams = new();

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

        if (!this.bufferedStreams.TryGetValue(relativeFilePath, out var streamWriter))
        {
            streamWriter = new StreamWriter(relativeFilePath, true);
            this.bufferedStreams.TryAdd(relativeFilePath, streamWriter);
        }

        streamWriter.Write(text);
    }

    public void WriteFile(string relativeFilePath, string text)
    {
        relativeFilePath = this.ResolveFilePath(relativeFilePath);

        lock (this.lockObject)
        {
            File.WriteAllText(relativeFilePath, text);
        }
    }

    public void WriteFile(FileInfo relativeFilePath, string text)
    {
        File.WriteAllText(relativeFilePath.FullName, text);
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

    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        foreach (var (filename, streamWriter) in this.bufferedStreams)
        {
            streamWriter.Dispose();
            this.bufferedStreams.TryRemove(filename, out _);
        }
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (filename, streamWriter) in this.bufferedStreams)
        {
            await streamWriter.DisposeAsync();
            this.bufferedStreams.TryRemove(filename, out _);
        }
    }
}

namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Exceptions;
using Newtonsoft.Json;

/// <inheritdoc />
public sealed class FileWritingService : IFileWritingService
{
    /// <summary>
    /// The format string used to generate the timestamp for the manifest file.
    /// </summary>
    public const string TimestampFormatString = "yyyyMMddHHmmssfff";
    private readonly ConcurrentDictionary<string, StreamWriter> bufferedStreams = new();

    private readonly object lockObject = new();
    private readonly string timestamp = DateTime.Now.ToString(TimestampFormatString, CultureInfo.InvariantCulture);

    /// <summary>
    /// The base path to write files to.
    /// If null or empty, the temp path will be used.
    /// </summary>
    public string BasePath { get; private set; }

    /// <inheritdoc />
    public void Init(string basePath)
    {
        if (!string.IsNullOrEmpty(basePath) && !Directory.Exists(basePath))
        {
            throw new InvalidUserInputException($"The path {basePath} does not exist.", new DirectoryNotFoundException());
        }

        this.BasePath = string.IsNullOrEmpty(basePath) ? Path.GetTempPath() : basePath;
    }

    /// <inheritdoc />
    public void AppendToFile<T>(string relativeFilePath, T obj)
    {
        relativeFilePath = this.ResolveFilePath(relativeFilePath);

        if (!this.bufferedStreams.TryGetValue(relativeFilePath, out var streamWriter))
        {
            streamWriter = new StreamWriter(relativeFilePath, true);
            _ = this.bufferedStreams.TryAdd(relativeFilePath, streamWriter);
        }

        var serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
        };
        serializer.Serialize(streamWriter, obj);
    }

    /// <inheritdoc />
    public void WriteFile(string relativeFilePath, string text)
    {
        relativeFilePath = this.ResolveFilePath(relativeFilePath);

        lock (this.lockObject)
        {
            File.WriteAllText(relativeFilePath, text);
        }
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string relativeFilePath, string text, CancellationToken cancellationToken = default)
    {
        relativeFilePath = this.ResolveFilePath(relativeFilePath);

        await File.WriteAllTextAsync(relativeFilePath, text, cancellationToken);
    }

    /// <inheritdoc />
    public void WriteFile<T>(FileInfo relativeFilePath, T obj)
    {
        using var streamWriter = new StreamWriter(relativeFilePath.FullName);
        using var jsonWriter = new JsonTextWriter(streamWriter);
        var serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
        };
        serializer.Serialize(jsonWriter, obj);
    }

    /// <inheritdoc />
    public string ResolveFilePath(string relativeFilePath)
    {
        this.EnsureInit();
        if (relativeFilePath.Contains("{timestamp}", StringComparison.Ordinal))
        {
            relativeFilePath = relativeFilePath.Replace("{timestamp}", this.timestamp, StringComparison.Ordinal);
        }

        relativeFilePath = Path.Combine(this.BasePath, relativeFilePath);
        return relativeFilePath;
    }

    /// <inheritdoc />
    public void Dispose() => this.Dispose(true);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var (filename, streamWriter) in this.bufferedStreams)
        {
            await streamWriter.DisposeAsync();
            _ = this.bufferedStreams.TryRemove(filename, out _);
        }
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
            _ = this.bufferedStreams.TryRemove(filename, out _);
        }
    }
}

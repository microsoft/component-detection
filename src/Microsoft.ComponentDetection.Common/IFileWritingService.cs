namespace Microsoft.ComponentDetection.Common;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides methods for writing files.
/// </summary>
public interface IFileWritingService : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Initializes the file writing service with the given base path.
    /// </summary>
    /// <param name="basePath">The base path to use for all file operations.</param>
    void Init(string basePath);

    /// <summary>
    /// Appends the object to the file as JSON.
    /// </summary>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <param name="obj">The object to append.</param>
    /// <typeparam name="T">The type of the object to append.</typeparam>
    void AppendToFile<T>(string relativeFilePath, T obj);

    /// <summary>
    /// Writes the text to the file.
    /// </summary>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <param name="text">The text to write.</param>
    void WriteFile(string relativeFilePath, string text);

    /// <summary>
    /// Writes the text to the file.
    /// </summary>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="cancellationToken">Token to cancel the file write operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task WriteFileAsync(string relativeFilePath, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the object to the file as JSON.
    /// </summary>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <param name="obj">The object to write.</param>
    /// <typeparam name="T">The type of the object to write.</typeparam>
    void WriteFile<T>(FileInfo relativeFilePath, T obj);

    /// <summary>
    /// Resolves the complete file path from the given relative file path.
    /// Replaces occurrences of {timestamp} with the shared file timestamp.
    /// </summary>
    /// <param name="relativeFilePath">The relative path to the file.</param>
    /// <returns>The complete file path.</returns>
    string ResolveFilePath(string relativeFilePath);
}

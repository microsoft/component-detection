#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.IO;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

/// <inheritdoc />
public class LazyComponentStream : IComponentStream
{
    private readonly FileInfo fileInfo;
    private readonly ILogger logger;
    private readonly Lazy<byte[]> fileBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyComponentStream"/> class.
    /// </summary>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="logger">The logger.</param>
    public LazyComponentStream(FileInfo fileInfo, string pattern, ILogger logger)
    {
        this.Pattern = pattern;
        this.Location = fileInfo.FullName;
        this.fileInfo = fileInfo;
        this.logger = logger;
        this.fileBuffer = new Lazy<byte[]>(this.SafeOpenFile);
    }

    /// <inheritdoc />
    public Stream Stream => new MemoryStream(this.fileBuffer.Value);

    /// <inheritdoc />
    public string Pattern { get; set; }

    /// <inheritdoc />
    public string Location { get; set; }

    private byte[] SafeOpenFile()
    {
        try
        {
            using var fs = this.fileInfo.OpenRead();

            var buffer = new byte[this.fileInfo.Length];
            fs.Read(buffer, 0, (int)this.fileInfo.Length);

            return buffer;
        }
        catch (UnauthorizedAccessException e)
        {
            this.logger.LogWarning(e, "Unauthorized access exception caught when trying to open {FileName}", this.fileInfo.FullName);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Unhandled exception caught when trying to open {FileName}", this.fileInfo.FullName);
        }

        return [];
    }
}

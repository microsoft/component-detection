#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class ComponentStreamEnumerable : IEnumerable<IComponentStream>
{
    private readonly ILogger logger;

    public ComponentStreamEnumerable(IEnumerable<MatchedFile> fileEnumerable, ILogger logger)
    {
        this.logger = logger;
        this.ToEnumerate = fileEnumerable;
    }

    private IEnumerable<MatchedFile> ToEnumerate { get; }

    public IEnumerator<IComponentStream> GetEnumerator()
    {
        foreach (var filePairing in this.ToEnumerate)
        {
            if (!filePairing.File.Exists)
            {
                this.logger.LogWarning("File {FilePairingName} does not exist on disk.", filePairing.File.FullName);
                yield break;
            }

            using var stream = this.SafeOpenFile(filePairing.File);

            if (stream == null)
            {
                yield break;
            }

            yield return new ComponentStream { Stream = stream, Pattern = filePairing.Pattern, Location = filePairing.File.FullName };
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    private Stream SafeOpenFile(FileInfo file)
    {
        try
        {
            return file.OpenRead();
        }
        catch (UnauthorizedAccessException)
        {
            this.logger.LogWarning("Unauthorized access exception caught when trying to open {FileName}", file.FullName);
            return null;
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Unhandled exception caught when trying to open {FileName}", file.FullName);
            return null;
        }
    }
}

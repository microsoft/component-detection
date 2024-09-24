namespace Microsoft.ComponentDetection.Detectors.Tests.Utilities;

using System;
using System.IO;

public sealed class TemporaryFile : IDisposable
{
    static TemporaryFile()
    {
        TemporaryDirectory = Path.Combine(Path.GetDirectoryName(typeof(TemporaryFile).Assembly.Location), "temporary-files");
        Directory.CreateDirectory(TemporaryDirectory);
    }

    // Creates a temporary file in the test directory with the optional given file extension.  The test/debug directory
    // is used to avoid polluting the user's temp directory and so that a `git clean` operation will remove any
    // remaining files.
    public TemporaryFile(string extension = null)
    {
        if (extension is not null && !extension.StartsWith("."))
        {
            throw new ArgumentException("Extension must start with a period.", nameof(extension));
        }

        this.FilePath = Path.Combine(TemporaryDirectory, $"{Guid.NewGuid():d}{extension}");
    }

    private static string TemporaryDirectory { get; }

    public string FilePath { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(this.FilePath);
        }
        catch
        {
        }
    }
}

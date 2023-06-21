namespace Microsoft.ComponentDetection.Common;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

/// <inheritdoc />
public class FileUtilityService : IFileUtilityService
{
    /// <inheritdoc />
    public string ReadAllText(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    /// <inheritdoc />
    public string ReadAllText(FileInfo file)
    {
        return File.ReadAllText(file.FullName);
    }

    /// <inheritdoc />
    public bool Exists(string fileName)
    {
        return File.Exists(fileName);
    }

    /// <inheritdoc />
    public Stream MakeFileStream(string fileName)
    {
        return new FileStream(fileName, FileMode.Open, FileAccess.Read);
    }
}

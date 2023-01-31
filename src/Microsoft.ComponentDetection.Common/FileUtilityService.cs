namespace Microsoft.ComponentDetection.Common;
using System.Composition;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Wraps some common file operations for easier testability. This interface is *only used by the command line driven app*.
/// </summary>
[Export(typeof(IFileUtilityService))]
[Export(typeof(FileUtilityService))]
[Shared]
public class FileUtilityService : IFileUtilityService
{
    public string ReadAllText(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    public string ReadAllText(FileInfo file)
    {
        return File.ReadAllText(file.FullName);
    }

    public bool Exists(string fileName)
    {
        return File.Exists(fileName);
    }

    public Stream MakeFileStream(string fileName)
    {
        return new FileStream(fileName, FileMode.Open, FileAccess.Read);
    }
}

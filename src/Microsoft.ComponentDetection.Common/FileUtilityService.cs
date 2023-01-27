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
    public string ReadAllText(string filePath) => File.ReadAllText(filePath);

    public string ReadAllText(FileInfo file) => File.ReadAllText(file.FullName);

    public bool Exists(string fileName) => File.Exists(fileName);

    public Stream MakeFileStream(string fileName) => new FileStream(fileName, FileMode.Open, FileAccess.Read);
}

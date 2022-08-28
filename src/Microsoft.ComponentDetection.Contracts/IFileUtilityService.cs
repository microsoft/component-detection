namespace Microsoft.ComponentDetection.Contracts
{
    using System.IO;

    /// <summary>
    /// Wraps some common file operations for easier testability. This interface is *only used by the command line driven app*.
    /// </summary>
    public interface IFileUtilityService
    {
        string ReadAllText(string filePath);

        string ReadAllText(FileInfo file);

        bool Exists(string fileName);

        Stream MakeFileStream(string fileName);
    }
}

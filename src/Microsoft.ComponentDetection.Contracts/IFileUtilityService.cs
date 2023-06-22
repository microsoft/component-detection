namespace Microsoft.ComponentDetection.Contracts;

using System.IO;

/// <summary>
/// Wraps some common file operations for easier testability. This interface is *only used by the command line driven app*.
/// </summary>
public interface IFileUtilityService
{
    /// <summary>
    /// Returns the contents of the file at the given path.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>Returns a string of the file contents.</returns>
    string ReadAllText(string filePath);

    /// <summary>
    /// Returns the contents of the file.
    /// </summary>
    /// <param name="file">File to read.</param>
    /// <returns>Returns a string of the file contents.</returns>
    string ReadAllText(FileInfo file);

    /// <summary>
    /// Returns true if the file exists.
    /// </summary>
    /// <param name="fileName">Path to the file.</param>
    /// <returns>Returns true if the file exists, otherwise false.</returns>
    bool Exists(string fileName);

    /// <summary>
    /// Returns a stream of the file.
    /// </summary>
    /// <param name="fileName">Path to the file.</param>
    /// <returns>Returns a stream representing the file.</returns>
    Stream MakeFileStream(string fileName);
}

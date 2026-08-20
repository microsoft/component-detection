#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.IO;
using System.Threading.Tasks;

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
    /// Returns the contents of the file.
    /// </summary>
    /// <param name="file">File to read.</param>
    /// <returns>Returns a string of the file contents.</returns>
    Task<string> ReadAllTextAsync(FileInfo file);

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

    /// <summary>
    /// Deletes the file at the specified path.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    void Delete(string path);

    /// <summary>
    /// Duplicates a file, removing any lines that start with the given string.
    /// </summary>
    /// <param name="fileName">Path to the file.</param>
    /// <param name="removalIndicators">The strings that indicate a line should be removed.</param>
    /// <returns>Returns the path of the new file, and whether or not one was created.</returns>
    (string DuplicateFilePath, bool CreatedDuplicate) DuplicateFileWithoutLines(string fileName, params string[] removalIndicators);
}

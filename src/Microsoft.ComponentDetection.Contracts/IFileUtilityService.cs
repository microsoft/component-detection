namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
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
    /// Duplicates a file, removing any lines that starts with the given string.
    /// </summary>
    /// <param name="fileName">Path to the file.</param>
    /// <param name="removalIndicators">The strings that indicates a line should be removed.</param>
    /// <returns>Returns the path of the new file, and whether or not one was created.</returns>
    (string DuplicateFilePath, bool CreatedDuplicate) DuplicateFileWithoutLines(string fileName, params string[] removalIndicators);

    /// <summary>
    /// Enumerates files and directories that match 'pattern' in directories up to 'depth'.
    /// </summary>
    /// <param name="root">Root path to begin traversal.</param>
    /// <param name="patterns">Patterns to match.</param>
    /// <param name="depth">Directory depth to search.</param>
    /// <returns>List of files and directories that match pattern.</returns>
    (HashSet<string> Files, HashSet<string> Directories) GetFilesAndDirectories(string root, IList<string> patterns, int depth);
}

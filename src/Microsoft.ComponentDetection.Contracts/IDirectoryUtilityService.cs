#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
using System.IO;

/// <summary>
/// Wraps some common directory operations for easier testability. This interface is *only used by the command line driven app*.
/// </summary>
public interface IDirectoryUtilityService
{
    /// <summary>
    /// Returns true if the directory exists.
    /// </summary>
    /// <param name="directoryPath">Path to the directory.</param>
    /// <returns>Returns true if the directory exists, otherwise false.</returns>
    bool Exists(string directoryPath);

    /// <summary>
    /// Enumerates directories in the specified path that match the specified search pattern.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search pattern to match against the names of directories.</param>
    /// <param name="searchOption">Specifies whether the search operation should include only the current directory or all subdirectories.</param>
    /// <returns>An enumerable collection of directory names that match the specified search pattern.</returns>
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Enumerates directories in the specified path.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <returns>An enumerable collection of directory names.</returns>
    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>
    /// Enumerates files in the specified path that match the specified search pattern.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search pattern to match against the names of files.</param>
    /// <param name="searchOption">Specifies whether the search operation should include only the current directory or all subdirectories.</param>
    /// <returns>An enumerable collection of file names that match the specified search pattern.</returns>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Deletes the specified directory and, if indicated, any subdirectories and files.
    /// </summary>
    /// <param name="path">Path to the directory.</param>
    /// <param name="recursive">True to remove directories, subdirectories, and files; otherwise, false.</param>
    /// <remarks>See <see cref="Directory.Delete(string, bool)"/> for more information.</remarks>
    void Delete(string path, bool recursive);

    /// <summary>
    /// Enumerates files and directories that match 'pattern' in directories up to 'depth'.
    /// </summary>
    /// <param name="root">Root path to begin traversal.</param>
    /// <param name="patterns">Patterns to match.</param>
    /// <param name="depth">Directory depth to search.</param>
    /// <returns>List of files and directories that match pattern.</returns>
    (HashSet<string> Files, HashSet<string> Directories) GetFilesAndDirectories(string root, IList<string> patterns, int depth);
}

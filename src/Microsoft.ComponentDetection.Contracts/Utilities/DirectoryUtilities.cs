namespace Microsoft.ComponentDetection.Contracts.Utilities;

using System.Collections.Generic;
using System.IO;

/// <summary>
/// Utility class for directory operations.
/// </summary>
public static class DirectoryUtilities
{
    /// <summary>
    /// Enumerates files and directories that match 'pattern' in directories up to 'depth'.
    /// </summary>
    /// <param name="root">Root path to begin traversal.</param>
    /// <param name="patterns">Patterns to match.</param>
    /// <param name="depth">Directory depth to search.</param>
    /// <returns>List of files and directories that match pattern.</returns>
    public static (HashSet<string> Files, HashSet<string> Directories) GetFilesAndDirectories(string root, IList<string> patterns, int depth)
    {
        var fileList = new List<string>();
        var dirList = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (depth > 0)
            {
                var (files, directories) = GetFilesAndDirectories(directory, patterns, depth - 1);
                fileList.AddRange(files);
                dirList.AddRange(directories);
            }
        }

        foreach (var pattern in patterns)
        {
            fileList.AddRange(Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly));
            dirList.AddRange(Directory.EnumerateDirectories(root, pattern, SearchOption.TopDirectoryOnly));
        }

        return (new HashSet<string>(fileList), new HashSet<string>(dirList));
    }
}

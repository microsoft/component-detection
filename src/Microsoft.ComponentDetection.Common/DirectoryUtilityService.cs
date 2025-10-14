#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

/// <inheritdoc />
public class DirectoryUtilityService : IDirectoryUtilityService
{
    /// <inheritdoc />
    public void Delete(string path, bool recursive) => Directory.Delete(path, recursive);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateDirectories(path, searchPattern, searchOption);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    /// <inheritdoc />
    public bool Exists(string directoryPath) => Directory.Exists(directoryPath);

    /// <inheritdoc />
    public (HashSet<string> Files, HashSet<string> Directories) GetFilesAndDirectories(string root, IList<string> patterns, int depth)
    {
        var fileList = new List<string>();
        var dirList = new List<string>();
        foreach (var directory in this.EnumerateDirectories(root))
        {
            if (depth > 0)
            {
                var (files, directories) = this.GetFilesAndDirectories(directory, patterns, depth - 1);
                fileList.AddRange(files);
                dirList.AddRange(directories);
            }
        }

        foreach (var pattern in patterns)
        {
            fileList.AddRange(this.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly));
            dirList.AddRange(this.EnumerateDirectories(root, pattern, SearchOption.TopDirectoryOnly));
        }

        return (new HashSet<string>(fileList), new HashSet<string>(dirList));
    }
}

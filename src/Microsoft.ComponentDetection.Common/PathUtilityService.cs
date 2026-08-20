#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.IO;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

// We may want to consider breaking this class into Win/Mac/Linux variants if it gets bigger
internal class PathUtilityService : IPathUtilityService
{
    public const uint CreationDispositionRead = 0x3;

    public const uint FileFlagBackupSemantics = 0x02000000;

    public const int InitalPathBufferSize = 512;

    public const string LongPathPrefix = "\\\\?\\";

    private readonly ILogger logger;

    public PathUtilityService(ILogger<PathUtilityService> logger) => this.logger = logger;

    public string GetParentDirectory(string path) => Path.GetDirectoryName(path);

    public bool IsFileBelowAnother(string aboveFilePath, string belowFilePath)
    {
        var aboveDirectoryPath = Path.GetDirectoryName(aboveFilePath);
        var belowDirectoryPath = Path.GetDirectoryName(belowFilePath);

        // Return true if they are not the same path but the second has the first as its base
        return (aboveDirectoryPath.Length != belowDirectoryPath.Length) && belowDirectoryPath.StartsWith(aboveDirectoryPath);
    }

    public string ResolvePhysicalPath(string path)
    {
        var directoryInfo = new DirectoryInfo(path);
        if (directoryInfo.Exists)
        {
            return this.ResolvePathFromInfo(directoryInfo);
        }

        var fileInfo = new FileInfo(path);
        return fileInfo.Exists ? this.ResolvePathFromInfo(fileInfo) : null;
    }

    [Obsolete("Use PatternMatchingUtility.MatchesPattern instead.")]
    public bool MatchesPattern(string pattern, string fileName) => PatternMatchingUtility.MatchesPattern(pattern, fileName);

    private string ResolvePathFromInfo(FileSystemInfo info) => info.LinkTarget ?? info.FullName;

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // Normalize the path directory seperator to / on Unix systems and on Windows.
        // This is the behavior we want as Windows accepts / as a separator.
        // AltDirectorySeparatorChar is / on Unix and on Windows.
        return path.Replace('\\', Path.AltDirectorySeparatorChar);
    }
}

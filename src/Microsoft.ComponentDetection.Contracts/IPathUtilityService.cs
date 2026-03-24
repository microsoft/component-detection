#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Wraps some common folder operations, shared across command line app and service.
/// </summary>
public interface IPathUtilityService
{
    /// <summary>
    /// Returns the parent directory of the given path.
    /// </summary>
    /// <param name="path">Path to get parent directory of.</param>
    /// <returns>Returns a string of the parent directory.</returns>
    string GetParentDirectory(string path);

    /// <summary>
    /// Given a path, resolve the underlying path, traversing any symlinks (man 2 lstat :D ).
    /// </summary>
    /// <param name="path">Path that needs to be resolved.</param>
    /// <returns>Returns a string of the underlying path.</returns>
    string ResolvePhysicalPath(string path);

    /// <summary>
    /// Returns true when the below file path exists under the above file path.
    /// </summary>
    /// <param name="aboveFilePath">The top file path.</param>
    /// <param name="belowFilePath">The file path to find within the top file path.</param>
    /// <returns>Returns true if the below file path exists under the above file path, otherwise false.</returns>
    bool IsFileBelowAnother(string aboveFilePath, string belowFilePath);

    /// <summary>
    /// Returns true if file name matches pattern.
    /// </summary>
    /// <param name="searchPattern">Search pattern.</param>
    /// <param name="fileName">File name without directory.</param>
    /// <returns>Returns true if file name matches a pattern, otherwise false.</returns>
    bool MatchesPattern(string searchPattern, string fileName);

    /// <summary>
    /// Normalize the path directory seperator to / on Unix systems and on Windows.
    /// This is the behavior we want as Windows accepts / as a separator.
    /// </summary>
    /// <param name="path">the path.</param>
    /// <returns>normalized path.</returns>
    string NormalizePath(string path);
}

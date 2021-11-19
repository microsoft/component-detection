namespace Microsoft.ComponentDetection.Contracts
{
    /// <summary>
    /// Wraps some common folder operations, shared across command line app and service.
    /// </summary>
    public interface IPathUtilityService
    {
        string GetParentDirectory(string path);

        /// <summary>
        /// Given a path, resolve the underlying path, traversing any symlinks (man 2 lstat :D ).
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        string ResolvePhysicalPath(string path);

        /// <summary>
        /// Returns true when the below file path exists under the above file path.
        /// </summary>
        /// <param name="aboveFilePath"></param>
        /// <param name="belowFilePath"></param>
        /// <returns></returns>
        bool IsFileBelowAnother(string aboveFilePath, string belowFilePath);

        /// <summary>
        /// Returns true if file name matches pattern.
        /// </summary>
        /// <param name="searchPattern">Search pattern.</param>
        /// <param name="fileName">File name without directory.</param>
        /// <returns></returns>
        bool MatchesPattern(string searchPattern, string fileName);
    }
}

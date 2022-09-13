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
        /// <param name="path">Path that needs to be resolved. </param>
        /// <returns> Returns a string of the underlying path. </returns>
        string ResolvePhysicalPath(string path);

        /// <summary>
        /// Returns true when the below file path exists under the above file path.
        /// </summary>
        /// <param name="aboveFilePath">The top file path. </param>
        /// <param name="belowFilePath">The file path to find within the top file path. </param>
        /// <returns> Return a bool. True, if below file path is found under above file path, otherwise false. </returns>
        bool IsFileBelowAnother(string aboveFilePath, string belowFilePath);

        /// <summary>
        /// Returns true if file name matches pattern.
        /// </summary>
        /// <param name="searchPattern">Search pattern.</param>
        /// <param name="fileName">File name without directory.</param>
        /// <returns>Returns true if file name matches a pattern, otherwise false. </returns>
        bool MatchesPattern(string searchPattern, string fileName);
    }
}

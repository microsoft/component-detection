namespace Microsoft.ComponentDetection.Detectors.DotNet;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Utility for rebasing absolute paths from one filesystem root to another.
/// </summary>
/// <remarks>
/// When component detection scans build outputs produced on a different machine (e.g., a CI agent),
/// the absolute paths recorded in artifacts like binlogs and project.assets.json will not match
/// the paths on the scanning machine. This utility detects and compensates for that by finding
/// the common relative suffix between two representations of the same directory and deriving
/// the root prefix that needs to be substituted.
/// </remarks>
internal static class PathRebasingUtility
{
    /// <summary>
    /// Normalizes a path by replacing backslashes with forward slashes.
    /// Windows accepts <c>/</c> as a separator, so forward-slash normalization works everywhere.
    /// </summary>
    /// <returns>The path with all backslashes replaced by forward slashes.</returns>
    internal static string NormalizePath(string path) => path.Replace('\\', Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Normalizes a directory path: forward slashes, no trailing separator.
    /// Returns null/empty passthrough for null/empty input.
    /// </summary>
    /// <returns>The normalized directory path, or <c>null</c>/<c>empty</c> for null/empty input.</returns>
    internal static string? NormalizeDirectory(string? path) =>
        string.IsNullOrEmpty(path) ? path : TrimAllEndingDirectorySeparators(NormalizePath(path));

    /// <summary>
    /// Given a path known to be under <paramref name="sourceDirectory"/> on the scanning machine,
    /// and the same directory as it appears in a build artifact (binlog, lock file, etc.),
    /// determine the root prefix in the artifact that corresponds to <paramref name="sourceDirectory"/>.
    /// </summary>
    /// <param name="sourceDirectory">The scanning machine's source directory (already normalized).</param>
    /// <param name="sourceDirectoryBasedPath">Path to a directory under <paramref name="sourceDirectory"/> on the scanning machine.</param>
    /// <param name="artifactPath">Path to the same directory as it appears in the build artifact.</param>
    /// <returns>
    /// The root prefix of <paramref name="artifactPath"/> that can be replaced with <paramref name="sourceDirectory"/>,
    /// or <c>null</c> if the paths cannot be rebased (same root, or no common relative suffix).
    /// </returns>
    internal static string? GetRebaseRoot(string? sourceDirectory, string sourceDirectoryBasedPath, string? artifactPath)
    {
        if (string.IsNullOrEmpty(artifactPath) || string.IsNullOrEmpty(sourceDirectory) || string.IsNullOrEmpty(sourceDirectoryBasedPath))
        {
            return null;
        }

        sourceDirectoryBasedPath = NormalizeDirectory(sourceDirectoryBasedPath)!;
        artifactPath = NormalizeDirectory(artifactPath)!;

        // Nothing to do if the paths are the same (no rebasing needed).
        if (artifactPath.Equals(sourceDirectoryBasedPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Find the relative path under sourceDirectory.
        var sourceDirectoryRelativePath = NormalizeDirectory(Path.GetRelativePath(sourceDirectory, sourceDirectoryBasedPath))!;

        // If the artifact path has the same relative portion, extract the root prefix.
        // Use case-insensitive comparison: Windows paths are case-insensitive, and on
        // Linux the paths will naturally have consistent casing.
        if (artifactPath.EndsWith(sourceDirectoryRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return artifactPath[..^sourceDirectoryRelativePath.Length];
        }

        // The path didn't have a common relative suffix — it might have been copied from
        // a completely different location since it was built. We cannot rebase.
        return null;
    }

    /// <summary>
    /// Rebases an absolute path from one root to another.
    /// </summary>
    /// <param name="path">The absolute path to rebase (from the build artifact).</param>
    /// <param name="originalRoot">The root prefix from the build artifact (as returned by <see cref="GetRebaseRoot"/>).</param>
    /// <param name="newRoot">The root on the scanning machine (typically sourceDirectory).</param>
    /// <returns>
    /// The rebased path under <paramref name="newRoot"/>, or the normalized input
    /// unchanged when it is not rooted or not under <paramref name="originalRoot"/>.
    /// </returns>
    internal static string RebasePath(string path, string originalRoot, string newRoot)
    {
        var normalizedPath = NormalizeDirectory(path)!;

        if (!Path.IsPathRooted(normalizedPath))
        {
            return normalizedPath;
        }

        var normalizedOriginal = NormalizeDirectory(originalRoot)!;
        var normalizedNew = NormalizeDirectory(newRoot)!;
        var relative = Path.GetRelativePath(normalizedOriginal, normalizedPath);

        // If the path is outside the original root the relative result will start
        // with ".." or remain rooted (Windows cross-drive). Return unchanged.
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return normalizedPath;
        }

        return NormalizePath(Path.Combine(normalizedNew, relative));
    }

    /// <summary>
    /// Searches a dictionary for a key that matches the given scan-machine path after rebasing.
    /// Computes the relative path of <paramref name="scanMachinePath"/> under <paramref name="sourceDirectory"/>
    /// and looks for a dictionary key whose normalized form ends with the same relative suffix.
    /// </summary>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="dictionary">The dictionary keyed by build-machine paths.</param>
    /// <param name="sourceDirectory">The scanning machine's source directory (normalized).</param>
    /// <param name="scanMachinePath">The path on the scanning machine to look up.</param>
    /// <param name="rebaseRoot">
    /// If a match is found, outputs the build-machine root prefix that can be used with <see cref="RebasePath"/>
    /// to convert other build-machine paths. <c>null</c> if no match is found.
    /// </param>
    /// <returns>The matched value, or <c>default</c> if no match is found.</returns>
    internal static TValue? FindByRelativePath<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> dictionary,
        string sourceDirectory,
        string scanMachinePath,
        out string? rebaseRoot)
    {
        rebaseRoot = null;

        var normalizedScanPath = NormalizePath(scanMachinePath);
        var relativePath = NormalizePath(Path.GetRelativePath(sourceDirectory, normalizedScanPath));

        // If the path isn't under sourceDirectory, we can't match by suffix.
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return default;
        }

        foreach (var kvp in dictionary)
        {
            var normalizedKey = NormalizePath(kvp.Key);
            if (normalizedKey.EndsWith(relativePath, StringComparison.OrdinalIgnoreCase))
            {
                // Derive the build-machine root from this match.
                rebaseRoot = normalizedKey[..^relativePath.Length];
                return kvp.Value;
            }
        }

        return default;
    }

    private static string TrimAllEndingDirectorySeparators(string path)
    {
        string last;

        do
        {
            last = path;
            path = Path.TrimEndingDirectorySeparator(last);
        }
        while (!ReferenceEquals(last, path));

        return path;
    }
}

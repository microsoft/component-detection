#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Factory for creating <see cref="IEnumerable{T}"/> of <see cref="IComponentStream"/> from a given directory.
/// </summary>
public interface IComponentStreamEnumerableFactory
{
    /// <summary>
    /// Returns an enumerable of <see cref="IComponentStream"/> which are representative of the contents of underlying files that matched the
    /// provided search pattern and exclusion function. Each stream is disposed on the end of a single iteration of a foreach loop.
    /// </summary>
    /// <param name="directory">The directory to search "from", e.g. the top level directory being searched.</param>
    /// <param name="searchPatterns">The patterns to use in the search.</param>
    /// <param name="directoryExclusionPredicate">Predicate which indicates which directories should be excluded.</param>
    /// <param name="recursivelyScanDirectories">Indicates whether the streams should enumerate files from sub directories.</param>
    /// <returns> Enumerable of <see cref="IComponentStream"/> files that matched the given search pattern and directory exclusion predicate.</returns>
    IEnumerable<IComponentStream> GetComponentStreams(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true);

    /// <summary>
    /// Returns an enumerable of <see cref="IComponentStream"/> which are representative of the contents of underlying files that matched the
    /// provided search and exclusion functions. Each stream is disposed on the end of a single iteration of a foreach loop.
    /// </summary>
    /// <param name="directory">The directory to search "from", e.g. the top level directory being searched.</param>
    /// <param name="fileMatchingPredicate">Predicate which indicates what files should be included.</param>
    /// <param name="directoryExclusionPredicate">Predicate which indicates which directories should be excluded.</param>
    /// <param name="recursivelyScanDirectories">Indicates whether the streams should enumerate files from sub directories.</param>
    /// <returns> Enumerable of <see cref="IComponentStream"/> files that matched the given file matching predicate and directory exclusion predicate. </returns>
    IEnumerable<IComponentStream> GetComponentStreams(DirectoryInfo directory, Func<FileInfo, bool> fileMatchingPredicate, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true);
}

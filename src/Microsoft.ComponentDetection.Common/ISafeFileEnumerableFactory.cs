namespace Microsoft.ComponentDetection.Common;

using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

/// <summary>Factory for generating safe file enumerables.</summary>
public interface ISafeFileEnumerableFactory
{
    /// <summary>Creates a "safe" file enumerable, which provides logging while evaluating search patterns on a known directory structure.</summary>
    /// <param name="directory">The directory to search "from", e.g. the top level directory being searched.</param>
    /// <param name="searchPatterns">The patterns to use in the search.</param>
    /// <param name="directoryExclusionPredicate">Predicate which indicates which directories should be excluded.</param>
    /// <returns>A FileInfo enumerable that should be iterated over, containing all valid files given the input patterns and directory exclusions.</returns>
    IEnumerable<MatchedFile> CreateSafeFileEnumerable(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate);
}

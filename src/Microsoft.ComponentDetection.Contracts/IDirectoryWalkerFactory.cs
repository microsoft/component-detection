namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

/// <summary>
/// A factory for creating directory walkers.
/// </summary>
public interface IDirectoryWalkerFactory
{
    /// <summary>
    /// Walks the directory tree rooted at <paramref name="root"/> and invokes <paramref name="callback"/> for each file that matches <paramref name="filePatterns"/>.
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="directoryExclusionPredicate">A predicate that determines whether a directory should be excluded from the search.</param>
    /// <param name="recorder">The component recorder.</param>
    /// <param name="callback">The callback to invoke for each file.</param>
    /// <param name="filePatterns">The file patterns to match.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WalkDirectoryAsync(DirectoryInfo root, ExcludeDirectoryPredicate directoryExclusionPredicate, IComponentRecorder recorder, Func<ProcessRequest, Task> callback, IEnumerable<string> filePatterns);
}

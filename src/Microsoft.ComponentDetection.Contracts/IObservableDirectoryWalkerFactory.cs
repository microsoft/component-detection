#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts.Internal;

public delegate bool ExcludeDirectoryPredicate(ReadOnlySpan<char> nameOfDirectoryToConsider, ReadOnlySpan<char> pathOfParentOfDirectoryToConsider);

public interface IObservableDirectoryWalkerFactory
{
    void Initialize(DirectoryInfo root, ExcludeDirectoryPredicate directoryExclusionPredicate, int count, IEnumerable<string> filePatterns = null);

    IObservable<ProcessRequest> GetFilteredComponentStreamObservable(DirectoryInfo root, IEnumerable<string> patterns, IComponentRecorder componentRecorder);
}

namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;

public interface IDirectoryWalkerFactory
{
    Task WalkDirectoryAsync(DirectoryInfo root, ExcludeDirectoryPredicate directoryExclusionPredicate, IComponentRecorder recorder, Func<ProcessRequest, Task> callback, IEnumerable<string> filePatterns);
}

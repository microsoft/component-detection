using System.Collections.Generic;
using System.Composition;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(ISafeFileEnumerableFactory))]
    [Shared]
    public class SafeFileEnumerableFactory : ISafeFileEnumerableFactory
    {
        [Import]
        public ILogger Logger { get; set; }

        [Import]
        public IPathUtilityService PathUtilityService { get; set; }

        public IEnumerable<MatchedFile> CreateSafeFileEnumerable(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate)
        {
            return new SafeFileEnumerable(directory, searchPatterns, Logger, PathUtilityService, directoryExclusionPredicate);
        }
    }
}
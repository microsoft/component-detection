namespace Microsoft.ComponentDetection.Common
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using Microsoft.ComponentDetection.Contracts;

    [Export(typeof(IComponentStreamEnumerableFactory))]
    [Shared]
    public class ComponentStreamEnumerableFactory : IComponentStreamEnumerableFactory
    {
        [Import]
        public ILogger Logger { get; set; }

        [Import]
        public IPathUtilityService PathUtilityService { get; set; }

        public IEnumerable<IComponentStream> GetComponentStreams(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true)
        {
            var enumerable = new SafeFileEnumerable(directory, searchPatterns, this.Logger, this.PathUtilityService, directoryExclusionPredicate, recursivelyScanDirectories);
            return new ComponentStreamEnumerable(enumerable, this.Logger);
        }

        public IEnumerable<IComponentStream> GetComponentStreams(DirectoryInfo directory, Func<FileInfo, bool> fileMatchingPredicate, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true)
        {
            var enumerable = new SafeFileEnumerable(directory, fileMatchingPredicate, this.Logger, this.PathUtilityService, directoryExclusionPredicate, recursivelyScanDirectories);
            return new ComponentStreamEnumerable(enumerable, this.Logger);
        }
    }
}

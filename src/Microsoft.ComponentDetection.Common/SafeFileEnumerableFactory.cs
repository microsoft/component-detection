namespace Microsoft.ComponentDetection.Common;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

[Export(typeof(ISafeFileEnumerableFactory))]
[Shared]
public class SafeFileEnumerableFactory : ISafeFileEnumerableFactory
{
    public SafeFileEnumerableFactory()
    {
    }

    public SafeFileEnumerableFactory(IPathUtilityService pathUtilityService, ILogger logger)
    {
        this.PathUtilityService = pathUtilityService;
        this.Logger = logger;
    }

    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IPathUtilityService PathUtilityService { get; set; }

    public IEnumerable<MatchedFile> CreateSafeFileEnumerable(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate)
    {
        return new SafeFileEnumerable(directory, searchPatterns, this.Logger, this.PathUtilityService, directoryExclusionPredicate);
    }
}

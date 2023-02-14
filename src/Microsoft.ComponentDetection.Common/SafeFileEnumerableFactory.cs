namespace Microsoft.ComponentDetection.Common;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

public class SafeFileEnumerableFactory : ISafeFileEnumerableFactory
{
    private readonly IPathUtilityService pathUtilityService;
    private readonly ILogger logger;

    public SafeFileEnumerableFactory()
    {
    }

    public SafeFileEnumerableFactory(IPathUtilityService pathUtilityService, ILogger logger)
    {
        this.pathUtilityService = pathUtilityService;
        this.logger = logger;
    }

    public IEnumerable<MatchedFile> CreateSafeFileEnumerable(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate)
    {
        return new SafeFileEnumerable(directory, searchPatterns, this.logger, this.pathUtilityService, directoryExclusionPredicate);
    }
}

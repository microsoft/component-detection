namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class ComponentStreamEnumerableFactory : IComponentStreamEnumerableFactory
{
    private readonly IPathUtilityService pathUtilityService;
    private readonly ILogger<ComponentStreamEnumerable> logger;

    public ComponentStreamEnumerableFactory(IPathUtilityService pathUtilityService, ILogger<ComponentStreamEnumerable> logger)
    {
        this.pathUtilityService = pathUtilityService;
        this.logger = logger;
    }

    public IEnumerable<IComponentStream> GetComponentStreams(DirectoryInfo directory, IEnumerable<string> searchPatterns, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true)
    {
        var enumerable = new SafeFileEnumerable(directory, searchPatterns, this.logger, this.pathUtilityService, directoryExclusionPredicate, recursivelyScanDirectories);
        return new ComponentStreamEnumerable(enumerable, this.logger);
    }

    public IEnumerable<IComponentStream> GetComponentStreams(DirectoryInfo directory, Func<FileInfo, bool> fileMatchingPredicate, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true)
    {
        var enumerable = new SafeFileEnumerable(directory, fileMatchingPredicate, this.logger, this.pathUtilityService, directoryExclusionPredicate, recursivelyScanDirectories);
        return new ComponentStreamEnumerable(enumerable, this.logger);
    }
}

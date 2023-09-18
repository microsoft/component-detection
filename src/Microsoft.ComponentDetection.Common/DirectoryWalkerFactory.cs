namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.Extensions.Logging;

public class DirectoryWalkerFactory : IDirectoryWalkerFactory
{
    private readonly IPathUtilityService pathUtilityService;
    private readonly ILogger<DirectoryWalkerFactory> logger;

    public DirectoryWalkerFactory(IPathUtilityService pathUtilityService, ILogger<DirectoryWalkerFactory> logger)
    {
        this.logger = logger;
        this.pathUtilityService = pathUtilityService;
    }

    public async Task WalkDirectoryAsync(DirectoryInfo root, ExcludeDirectoryPredicate directoryExclusionPredicate, IComponentRecorder recorder, Func<ProcessRequest, Task> callback, IEnumerable<string> filePatterns)
    {
        if (!root.Exists)
        {
            this.logger.LogError("Root directory doesn't exist: {RootFullName}", root.FullName);
            return;
        }

        PatternMatchingUtility.FilePatternMatcher fileIsMatch = null;

        if (filePatterns == null || !filePatterns.Any())
        {
            fileIsMatch = _ => true;
        }
        else
        {
            fileIsMatch = PatternMatchingUtility.GetFilePatternMatcher(filePatterns);
        }

        var actionBlock = new ActionBlock<ProcessRequest>(callback, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount });

        var walker = new FileSystemEnumerable<FileSystemInfo>(
            root.FullName,
            (ref FileSystemEntry entry) => entry.ToFileSystemInfo(),
            new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                RecurseSubdirectories = true,
                MatchCasing = MatchCasing.CaseInsensitive,
                MatchType = MatchType.Simple,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
            });

        var scannedDirectories = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        walker.ShouldRecursePredicate = (ref FileSystemEntry entry) =>
        {
            if (entry.ToFileSystemInfo() is not DirectoryInfo di)
            {
                return false;
            }

            var realDirectory = di;

            if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                var realPath = this.pathUtilityService.ResolvePhysicalPath(di.FullName);

                realDirectory = new DirectoryInfo(realPath);
            }

            if (!scannedDirectories.TryAdd(realDirectory.FullName, true))
            {
                return false;
            }

            var skip = !directoryExclusionPredicate(entry.FileName.ToString(), entry.Directory.ToString());

            if (skip)
            {
                this.logger.LogTrace("Skipping directory {FullName}", di.FullName);
            }

            return skip;
        };
        foreach (var entry in walker)
        {
            if (entry is not FileInfo fi)
            {
                continue;
            }

            if (!fileIsMatch(fi.Name))
            {
                continue;
            }

            // We have a file to process
            foreach (var pattern in filePatterns)
            {
                if (!this.pathUtilityService.MatchesPattern(pattern, fi.Name))
                {
                    continue;
                }

                this.logger.LogDebug("Processing file {FullName}", fi.FullName);

                var lcs = new LazyComponentStream(fi, pattern, this.logger);
                var processRequest = new ProcessRequest
                {
                    ComponentStream = lcs,
                    SingleFileComponentRecorder = recorder.CreateSingleFileComponentRecorder(lcs.Location),
                };

                await actionBlock.SendAsync(processRequest);
            }
        }

        actionBlock.Complete();
        await actionBlock.Completion;
    }
}

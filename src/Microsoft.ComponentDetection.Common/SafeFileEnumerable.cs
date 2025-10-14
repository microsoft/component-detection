#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class SafeFileEnumerable : IEnumerable<MatchedFile>
{
    private readonly IEnumerable<string> searchPatterns;
    private readonly ExcludeDirectoryPredicate directoryExclusionPredicate;
    private readonly DirectoryInfo directory;
    private readonly IPathUtilityService pathUtilityService;
    private readonly bool recursivelyScanDirectories;
    private readonly Func<FileInfo, bool> fileMatchingPredicate;

    private readonly EnumerationOptions enumerationOptions;

    private readonly HashSet<string> enumeratedDirectories;
    private readonly ILogger logger;

    public SafeFileEnumerable(DirectoryInfo directory, IEnumerable<string> searchPatterns, ILogger logger, IPathUtilityService pathUtilityService, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true, HashSet<string> previouslyEnumeratedDirectories = null)
    {
        this.directory = directory;
        this.logger = logger;
        this.searchPatterns = searchPatterns;
        this.directoryExclusionPredicate = directoryExclusionPredicate;
        this.recursivelyScanDirectories = recursivelyScanDirectories;
        this.pathUtilityService = pathUtilityService;
        this.enumeratedDirectories = previouslyEnumeratedDirectories;

        this.enumerationOptions = new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = this.recursivelyScanDirectories,
            ReturnSpecialDirectories = false,
            MatchType = MatchType.Simple,
        };
    }

    public SafeFileEnumerable(DirectoryInfo directory, Func<FileInfo, bool> fileMatchingPredicate, ILogger logger, IPathUtilityService pathUtilityService, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true, HashSet<string> previouslyEnumeratedDirectories = null)
        : this(directory, ["*"], logger, pathUtilityService, directoryExclusionPredicate, recursivelyScanDirectories, previouslyEnumeratedDirectories) => this.fileMatchingPredicate = fileMatchingPredicate;

    public IEnumerator<MatchedFile> GetEnumerator()
    {
        var previouslyEnumeratedDirectories = this.enumeratedDirectories ?? [];

        var fse = new FileSystemEnumerable<MatchedFile>(
            this.directory.FullName,
            (ref FileSystemEntry entry) =>
            {
                if (!(entry.ToFileSystemInfo() is FileInfo fi))
                {
                    throw new InvalidOperationException("Encountered directory when expecting a file");
                }

                var foundPattern = entry.FileName.ToString();
                foreach (var searchPattern in this.searchPatterns)
                {
                    if (PathUtilityService.MatchesPattern(searchPattern, ref entry))
                    {
                        foundPattern = searchPattern;
                    }
                }

                return new MatchedFile() { File = fi, Pattern = foundPattern };
            },
            this.enumerationOptions)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
            {
                if (entry.IsDirectory)
                {
                    return false;
                }

                foreach (var searchPattern in this.searchPatterns)
                {
                    if (PathUtilityService.MatchesPattern(searchPattern, ref entry))
                    {
                        return true;
                    }
                }

                return false;
            },
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
            {
                if (!this.recursivelyScanDirectories)
                {
                    return false;
                }

                var targetPath = entry.ToFullPath();

                var seenPreviously = false;

                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var realPath = this.pathUtilityService.ResolvePhysicalPath(targetPath);

                    seenPreviously = previouslyEnumeratedDirectories.Contains(realPath);
                    previouslyEnumeratedDirectories.Add(realPath);

                    if (realPath.StartsWith(targetPath))
                    {
                        return false;
                    }
                }
                else if (previouslyEnumeratedDirectories.Contains(targetPath))
                {
                    seenPreviously = true;
                }

                previouslyEnumeratedDirectories.Add(targetPath);

                if (seenPreviously)
                {
                    this.logger.LogDebug("Encountered real path {TargetPath} before. Short-Circuiting directory traversal", targetPath);
                    return false;
                }

                // This is actually a *directory* name (not FileName) and the directory containing that directory.
                if (entry.IsDirectory && this.directoryExclusionPredicate != null && this.directoryExclusionPredicate(entry.FileName, entry.Directory))
                {
                    return false;
                }

                return true;
            },
        };

        foreach (var file in fse)
        {
            if (this.fileMatchingPredicate == null || this.fileMatchingPredicate(file.File))
            {
                yield return file;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}

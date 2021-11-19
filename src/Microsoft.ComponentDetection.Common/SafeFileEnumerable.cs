using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    public class SafeFileEnumerable : IEnumerable<MatchedFile>
    {
        private HashSet<string> enumeratedDirectories;

        private readonly IEnumerable<string> searchPatterns;
        private readonly ExcludeDirectoryPredicate directoryExclusionPredicate;
        private readonly DirectoryInfo directory;
        private readonly ILogger logger;
        private readonly IPathUtilityService pathUtilityService;
        private readonly bool recursivelyScanDirectories;
        private readonly Func<FileInfo, bool> fileMatchingPredicate;

        private readonly EnumerationOptions enumerationOptions;

        public SafeFileEnumerable(DirectoryInfo directory, IEnumerable<string> searchPatterns, ILogger logger, IPathUtilityService pathUtilityService, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true, HashSet<string> previouslyEnumeratedDirectories = null)
        {
            this.directory = directory;
            this.logger = logger;
            this.searchPatterns = searchPatterns;
            this.directoryExclusionPredicate = directoryExclusionPredicate;
            this.recursivelyScanDirectories = recursivelyScanDirectories;
            this.pathUtilityService = pathUtilityService;
            enumeratedDirectories = previouslyEnumeratedDirectories;

            enumerationOptions = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = this.recursivelyScanDirectories,
                ReturnSpecialDirectories = false,
                MatchType = MatchType.Simple,
            };
        }

        public SafeFileEnumerable(DirectoryInfo directory, Func<FileInfo, bool> fileMatchingPredicate, ILogger logger, IPathUtilityService pathUtilityService, ExcludeDirectoryPredicate directoryExclusionPredicate, bool recursivelyScanDirectories = true, HashSet<string> previouslyEnumeratedDirectories = null)
            : this(directory, new List<string> { "*" }, logger, pathUtilityService, directoryExclusionPredicate, recursivelyScanDirectories, previouslyEnumeratedDirectories)
        {
            this.fileMatchingPredicate = fileMatchingPredicate;
        }

        public IEnumerator<MatchedFile> GetEnumerator()
        {
            var previouslyEnumeratedDirectories = enumeratedDirectories ?? new HashSet<string>();

            var fse = new FileSystemEnumerable<MatchedFile>(directory.FullName, (ref FileSystemEntry entry) =>
            {
                if (!(entry.ToFileSystemInfo() is FileInfo fi))
                {
                    throw new InvalidOperationException("Encountered directory when expecting a file");
                }

                var foundPattern = entry.FileName.ToString();
                foreach (var searchPattern in searchPatterns)
                {
                    if (PathUtilityService.MatchesPattern(searchPattern, ref entry))
                    {
                        foundPattern = searchPattern;
                    }
                }

                return new MatchedFile() { File = fi, Pattern = foundPattern };
            }, enumerationOptions)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                {
                    if (entry.IsDirectory)
                    {
                        return false;
                    }

                    foreach (var searchPattern in searchPatterns)
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
                    if (!recursivelyScanDirectories)
                    {
                        return false;
                    }

                    var targetPath = entry.ToFullPath();

                    var seenPreviously = false;

                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        var realPath = pathUtilityService.ResolvePhysicalPath(targetPath);

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
                        logger.LogVerbose($"Encountered real path {targetPath} before. Short-Circuiting directory traversal");
                        return false;
                    }

                    // This is actually a *directory* name (not FileName) and the directory containing that directory.
                    if (entry.IsDirectory && directoryExclusionPredicate != null && directoryExclusionPredicate(entry.FileName, entry.Directory))
                    {
                        return false;
                    }

                    return true;
                },
            };

            foreach (var file in fse)
            {
                if (fileMatchingPredicate == null || fileMatchingPredicate(file.File))
                {
                    yield return file;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
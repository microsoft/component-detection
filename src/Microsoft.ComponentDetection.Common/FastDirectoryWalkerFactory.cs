namespace Microsoft.ComponentDetection.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;

[Export(typeof(IObservableDirectoryWalkerFactory))]
[Export(typeof(FastDirectoryWalkerFactory))]
[Shared]
public class FastDirectoryWalkerFactory : IObservableDirectoryWalkerFactory
{
    private readonly ConcurrentDictionary<DirectoryInfo, Lazy<IObservable<FileSystemInfo>>> pendingScans = new();

    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IPathUtilityService PathUtilityService { get; set; }

    public IObservable<FileSystemInfo> GetDirectoryScanner(DirectoryInfo root, ConcurrentDictionary<string, bool> scannedDirectories, ExcludeDirectoryPredicate directoryExclusionPredicate, IEnumerable<string> filePatterns = null, bool recurse = true) => Observable.Create<FileSystemInfo>(s =>
                                                                                                                                                                                                                                                                   {
                                                                                                                                                                                                                                                                       if (!root.Exists)
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           this.Logger?.LogError($"Root directory doesn't exist: {root.FullName}");
                                                                                                                                                                                                                                                                           s.OnCompleted();
                                                                                                                                                                                                                                                                           return Task.CompletedTask;
                                                                                                                                                                                                                                                                       }

                                                                                                                                                                                                                                                                       PatternMatchingUtility.FilePatternMatcher fileIsMatch = null;

                                                                                                                                                                                                                                                                       if (filePatterns == null || !filePatterns.Any())
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           fileIsMatch = span => true;
                                                                                                                                                                                                                                                                       }
                                                                                                                                                                                                                                                                       else
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           fileIsMatch = PatternMatchingUtility.GetFilePatternMatcher(filePatterns);
                                                                                                                                                                                                                                                                       }

                                                                                                                                                                                                                                                                       var sw = Stopwatch.StartNew();

                                                                                                                                                                                                                                                                       this.Logger?.LogInfo($"Starting enumeration of {root.FullName}");

                                                                                                                                                                                                                                                                       var fileCount = 0;
                                                                                                                                                                                                                                                                       var directoryCount = 0;

                                                                                                                                                                                                                                                                       var shouldRecurse = new FileSystemEnumerable<FileSystemInfo>.FindPredicate((ref FileSystemEntry entry) =>
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           if (!recurse)
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               return false;
                                                                                                                                                                                                                                                                           }

                                                                                                                                                                                                                                                                           if (!(entry.ToFileSystemInfo() is DirectoryInfo di))
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               return false;
                                                                                                                                                                                                                                                                           }

                                                                                                                                                                                                                                                                           var realDirectory = di;

                                                                                                                                                                                                                                                                           if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               var realPath = this.PathUtilityService.ResolvePhysicalPath(di.FullName);

                                                                                                                                                                                                                                                                               realDirectory = new DirectoryInfo(realPath);
                                                                                                                                                                                                                                                                           }

                                                                                                                                                                                                                                                                           if (!scannedDirectories.TryAdd(realDirectory.FullName, true))
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               return false;
                                                                                                                                                                                                                                                                           }

                                                                                                                                                                                                                                                                           if (directoryExclusionPredicate != null)
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               return !directoryExclusionPredicate(entry.FileName.ToString(), entry.Directory.ToString());
                                                                                                                                                                                                                                                                           }

                                                                                                                                                                                                                                                                           return true;
                                                                                                                                                                                                                                                                       });

                                                                                                                                                                                                                                                                       var initialIterator = new FileSystemEnumerable<FileSystemInfo>(root.FullName, this.Transform, new EnumerationOptions()
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           RecurseSubdirectories = false,
                                                                                                                                                                                                                                                                           IgnoreInaccessible = true,
                                                                                                                                                                                                                                                                           ReturnSpecialDirectories = false,
                                                                                                                                                                                                                                                                       })
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               if (!entry.IsDirectory && fileIsMatch(entry.FileName))
                                                                                                                                                                                                                                                                               {
                                                                                                                                                                                                                                                                                   return true;
                                                                                                                                                                                                                                                                               }

                                                                                                                                                                                                                                                                               return shouldRecurse(ref entry);
                                                                                                                                                                                                                                                                           },
                                                                                                                                                                                                                                                                       };

                                                                                                                                                                                                                                                                       var observables = new List<IObservable<FileSystemInfo>>();

                                                                                                                                                                                                                                                                       var initialFiles = new List<FileInfo>();
                                                                                                                                                                                                                                                                       var initialDirectories = new List<DirectoryInfo>();

                                                                                                                                                                                                                                                                       foreach (var fileSystemInfo in initialIterator)
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           if (fileSystemInfo is FileInfo fi)
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               initialFiles.Add(fi);
                                                                                                                                                                                                                                                                           }
                                                                                                                                                                                                                                                                           else if (fileSystemInfo is DirectoryInfo di)
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               initialDirectories.Add(di);
                                                                                                                                                                                                                                                                           }
                                                                                                                                                                                                                                                                       }

                                                                                                                                                                                                                                                                       observables.Add(Observable.Create<FileSystemInfo>(sub =>
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           foreach (var fileInfo in initialFiles)
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               sub.OnNext(fileInfo);
                                                                                                                                                                                                                                                                           }

                                                                                                                                                                                                                                                                           sub.OnCompleted();

                                                                                                                                                                                                                                                                           return Task.CompletedTask;
                                                                                                                                                                                                                                                                       }));

                                                                                                                                                                                                                                                                       if (recurse)
                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                           observables.Add(Observable.Create<FileSystemInfo>(async observer =>
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               var scan = new ActionBlock<DirectoryInfo>(
                                                                                                                                                                                                                                                                                   di =>
                                                                                                                                                                                                                                                                                   {
                                                                                                                                                                                                                                                                                       var enumerator = new FileSystemEnumerable<FileSystemInfo>(di.FullName, this.Transform, new EnumerationOptions()
                                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                                           RecurseSubdirectories = true,
                                                                                                                                                                                                                                                                                           IgnoreInaccessible = true,
                                                                                                                                                                                                                                                                                           ReturnSpecialDirectories = false,
                                                                                                                                                                                                                                                                                       })
                                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                                           ShouldRecursePredicate = shouldRecurse,
                                                                                                                                                                                                                                                                                       };

                                                                                                                                                                                                                                                                                       foreach (var fileSystemInfo in enumerator)
                                                                                                                                                                                                                                                                                       {
                                                                                                                                                                                                                                                                                           observer.OnNext(fileSystemInfo);
                                                                                                                                                                                                                                                                                       }
                                                                                                                                                                                                                                                                                   },
                                                                                                                                                                                                                                                                                   new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount });

                                                                                                                                                                                                                                                                               foreach (var directoryInfo in initialDirectories)
                                                                                                                                                                                                                                                                               {
                                                                                                                                                                                                                                                                                   scan.Post(directoryInfo);
                                                                                                                                                                                                                                                                               }

                                                                                                                                                                                                                                                                               scan.Complete();
                                                                                                                                                                                                                                                                               await scan.Completion;
                                                                                                                                                                                                                                                                               observer.OnCompleted();
                                                                                                                                                                                                                                                                           }));
                                                                                                                                                                                                                                                                       }

                                                                                                                                                                                                                                                                       return observables.Concat().Subscribe(
                                                                                                                                                                                                                                                                           info =>
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               if (info is FileInfo)
                                                                                                                                                                                                                                                                               {
                                                                                                                                                                                                                                                                                   Interlocked.Increment(ref fileCount);
                                                                                                                                                                                                                                                                               }
                                                                                                                                                                                                                                                                               else
                                                                                                                                                                                                                                                                               {
                                                                                                                                                                                                                                                                                   Interlocked.Increment(ref directoryCount);
                                                                                                                                                                                                                                                                               }

                                                                                                                                                                                                                                                                               s.OnNext(info);
                                                                                                                                                                                                                                                                           },
                                                                                                                                                                                                                                                                           () =>
                                                                                                                                                                                                                                                                           {
                                                                                                                                                                                                                                                                               sw.Stop();
                                                                                                                                                                                                                                                                               this.Logger?.LogInfo($"Enumerated {fileCount} files and {directoryCount} directories in {sw.Elapsed}");
                                                                                                                                                                                                                                                                               s.OnCompleted();
                                                                                                                                                                                                                                                                           });
                                                                                                                                                                                                                                                                   });

    /// <summary>
    /// Initialized an observable file enumerator.
    /// </summary>
    /// <param name="root">Root directory to scan.</param>
    /// <param name="directoryExclusionPredicate">predicate for excluding directories.</param>
    /// <param name="minimumConnectionCount">Number of observers that need to subscribe before the observable connects and starts enumerating.</param>
    /// <param name="filePatterns">Pattern used to filter files.</param>
    public void Initialize(DirectoryInfo root, ExcludeDirectoryPredicate directoryExclusionPredicate, int minimumConnectionCount, IEnumerable<string> filePatterns = null) => this.pendingScans.GetOrAdd(root, new Lazy<IObservable<FileSystemInfo>>(() => this.CreateDirectoryWalker(root, directoryExclusionPredicate, minimumConnectionCount, filePatterns)));

    public IObservable<FileSystemInfo> Subscribe(DirectoryInfo root, IEnumerable<string> patterns)
    {
        var patternArray = patterns.ToArray();

        if (this.pendingScans.TryGetValue(root, out var scannerObservable))
        {
            this.Logger.LogVerbose(string.Join(":", patterns));

            var inner = scannerObservable.Value.Where(fsi =>
            {
                if (fsi is FileInfo fi)
                {
                    return this.MatchesAnyPattern(fi, patternArray);
                }
                else
                {
                    return true;
                }
            });

            return inner;
        }

        throw new InvalidOperationException("Subscribe called without initializing scanner");
    }

    public IObservable<ProcessRequest> GetFilteredComponentStreamObservable(DirectoryInfo root, IEnumerable<string> patterns, IComponentRecorder componentRecorder)
    {
        var observable = this.Subscribe(root, patterns).OfType<FileInfo>().SelectMany(f => patterns.Select(sp => new
        {
            SearchPattern = sp,
            File = f,
        })).Where(x =>
            {
                var searchPattern = x.SearchPattern;
                var fileName = x.File.Name;

                return this.PathUtilityService.MatchesPattern(searchPattern, fileName);
            }).Where(x => x.File.Exists)
            .Select(x =>
            {
                var lazyComponentStream = new LazyComponentStream(x.File, x.SearchPattern, this.Logger);
                return new ProcessRequest
                {
                    ComponentStream = lazyComponentStream,
                    SingleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(lazyComponentStream.Location),
                };
            });

        return observable;
    }

    public void StartScan(DirectoryInfo root)
    {
        if (this.pendingScans.TryRemove(root, out var scannerObservable))
        {
            // scannerObservable.Connect();
        }

        throw new InvalidOperationException("StartScan called without initializing scanner");
    }

    public void Reset() => this.pendingScans.Clear();

    private FileSystemInfo Transform(ref FileSystemEntry entry) => entry.ToFileSystemInfo();

    private IObservable<FileSystemInfo> CreateDirectoryWalker(DirectoryInfo di, ExcludeDirectoryPredicate directoryExclusionPredicate, int minimumConnectionCount, IEnumerable<string> filePatterns) => this.GetDirectoryScanner(di, new ConcurrentDictionary<string, bool>(), directoryExclusionPredicate, filePatterns, true).Replay() // Returns a replay subject which will republish anything found to new subscribers.
            .AutoConnect(minimumConnectionCount); // Specifies that this connectable observable should start when minimumConnectionCount subscribe.

    private bool MatchesAnyPattern(FileInfo fi, params string[] searchPatterns) => searchPatterns != null && searchPatterns.Any(sp => this.PathUtilityService.MatchesPattern(sp, fi.Name));
}

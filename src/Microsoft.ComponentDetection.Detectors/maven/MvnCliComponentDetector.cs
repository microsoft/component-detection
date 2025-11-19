#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class MvnCliComponentDetector : FileComponentDetector
{
    private const string MavenManifest = "pom.xml";

    private readonly IMavenCommandService mavenCommandService;

    public MvnCliComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IMavenCommandService mavenCommandService,
        ILogger<MvnCliComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.mavenCommandService = mavenCommandService;
        this.Logger = logger;
    }

    public override string Id => "MvnCli";

    public override IList<string> SearchPatterns => [MavenManifest];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Maven];

    public override int Version => 4;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Maven)];

    private void LogDebugWithId(string message)
    {
        this.Logger.LogDebug("{DetectorId} detector: {Message}", this.Id, message);
    }

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        if (!await this.mavenCommandService.MavenCLIExistsAsync())
        {
            this.LogDebugWithId("Skipping maven detection as maven is not available in the local PATH.");
            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        var processPomFile = new ActionBlock<ProcessRequest>(x => this.mavenCommandService.GenerateDependenciesFileAsync(x, cancellationToken));

        await this.RemoveNestedPomXmls(processRequests).ForEachAsync(processRequest =>
        {
            processPomFile.Post(processRequest);
        });

        processPomFile.Complete();

        await processPomFile.Completion;

        this.LogDebugWithId($"Nested {MavenManifest} files processed successfully, retrieving generated dependency graphs.");

        return this.ComponentStreamEnumerableFactory.GetComponentStreams(this.CurrentScanRequest.SourceDirectory, [this.mavenCommandService.BcdeMvnDependencyFileName], this.CurrentScanRequest.DirectoryExclusionPredicate)
            .Select(componentStream =>
            {
                // The file stream is going to be disposed after the iteration is finished
                // so is necessary to read the content and keep it in memory, for further processing.
                using var reader = new StreamReader(componentStream.Stream);
                var content = reader.ReadToEnd();
                return new ProcessRequest
                {
                    ComponentStream = new ComponentStream
                    {
                        Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                        Location = componentStream.Location,
                        Pattern = componentStream.Pattern,
                    },
                    SingleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(
                        Path.Combine(Path.GetDirectoryName(componentStream.Location), MavenManifest)),
                };
            })
            .ToObservable();
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        this.mavenCommandService.ParseDependenciesFile(processRequest);

        File.Delete(processRequest.ComponentStream.Location);

        await Task.CompletedTask;
    }

    private IObservable<ProcessRequest> RemoveNestedPomXmls(IObservable<ProcessRequest> componentStreams)
    {
        var directoryItemFacades = new ConcurrentDictionary<string, DirectoryItemFacadeOptimized>(StringComparer.OrdinalIgnoreCase);
        var topLevelDirectories = new ConcurrentDictionary<string, DirectoryItemFacadeOptimized>(StringComparer.OrdinalIgnoreCase);

        return Observable.Create<ProcessRequest>(s =>
        {
            return componentStreams.Subscribe(
                processRequest =>
                {
                    var item = processRequest.ComponentStream;
                    var currentDir = item.Location;
                    DirectoryItemFacadeOptimized last = null;
                    while (!string.IsNullOrWhiteSpace(currentDir))
                    {
                        currentDir = Path.GetDirectoryName(currentDir);

                        // We've reached the top / root
                        if (string.IsNullOrWhiteSpace(currentDir))
                        {
                            // If our last directory isn't in our list of top level nodes, it should be added. This happens for the first processed item and then subsequent times we have a new root (edge cases with multiple hard drives, for example)
                            if (last != null && !topLevelDirectories.ContainsKey(last.Name))
                            {
                                topLevelDirectories.TryAdd(last.Name, last);
                            }

                            this.LogDebugWithId($"Discovered {item.Location}.");

                            // If we got to the top without finding a directory that had a pom.xml on the way, we yield.
                            s.OnNext(processRequest);
                            break;
                        }

                        var current = directoryItemFacades.GetOrAdd(currentDir, _ => new DirectoryItemFacadeOptimized
                        {
                            Name = currentDir,
                            FileNames = [],
                        });

                        // If we didn't come from a directory, it's because we're just getting started. Our current directory should include the file that led to it showing up in the graph.
                        if (last == null)
                        {
                            current.FileNames.Add(Path.GetFileName(item.Location));
                        }

                        if (last != null && current.FileNames.Contains(MavenManifest))
                        {
                            this.LogDebugWithId($"Ignoring {MavenManifest} at {item.Location}, as it has a parent {MavenManifest} that will be processed at {current.Name}\\{MavenManifest} .");
                            break;
                        }

                        last = current;
                    }
                },
                s.OnCompleted);
        });
    }
}

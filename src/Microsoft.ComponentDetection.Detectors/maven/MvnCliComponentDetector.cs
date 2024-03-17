namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class MvnCliComponentDetector : FileComponentDetector
{
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

    public override IList<string> SearchPatterns => new List<string> { "pom.xml" };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Maven };

    public override int Version => 3;

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Maven) };

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
    {
        if (!await this.mavenCommandService.MavenCLIExistsAsync())
        {
            this.Logger.LogDebug("Skipping maven detection as maven is not available in the local PATH.");
            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        var processPomFile = new ActionBlock<ProcessRequest>(this.mavenCommandService.GenerateDependenciesFileAsync);

        await this.RemoveNestedPomXmls(processRequests).ForEachAsync(processRequest =>
        {
            processPomFile.Post(processRequest);
        });

        processPomFile.Complete();

        await processPomFile.Completion;

        return this.ComponentStreamEnumerableFactory.GetComponentStreams(this.CurrentScanRequest.SourceDirectory, new[] { this.mavenCommandService.BcdeMvnDependencyFileName }, this.CurrentScanRequest.DirectoryExclusionPredicate)
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
                        Path.Combine(Path.GetDirectoryName(componentStream.Location), "pom.xml")),
                };
            })
            .ToObservable();
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        this.mavenCommandService.ParseDependenciesFile(processRequest);

        File.Delete(processRequest.ComponentStream.Location);

        await Task.CompletedTask;
    }

    private IObservable<ProcessRequest> RemoveNestedPomXmls(IObservable<ProcessRequest> componentStreams)
    {
        var directoryItemFacades = new List<DirectoryItemFacade>();
        var directoryItemFacadesByPath = new Dictionary<string, DirectoryItemFacade>();
        return Observable.Create<ProcessRequest>(s =>
        {
            return componentStreams.Subscribe(
                processRequest =>
                {
                    var item = processRequest.ComponentStream;
                    var currentDir = item.Location;
                    DirectoryItemFacade last = null;
                    do
                    {
                        currentDir = Path.GetDirectoryName(currentDir);

                        // We've reached the top / root
                        if (currentDir == null)
                        {
                            // If our last directory isn't in our list of top level nodes, it should be added. This happens for the first processed item and then subsequent times we have a new root (edge cases with multiple hard drives, for example)
                            if (!directoryItemFacades.Contains(last))
                            {
                                directoryItemFacades.Add(last);
                            }

                            // If we got to the top without finding a directory that had a pom.xml on the way, we yield.
                            s.OnNext(processRequest);
                            break;
                        }

                        var directoryExisted = directoryItemFacadesByPath.TryGetValue(currentDir, out var current);
                        if (!directoryExisted)
                        {
                            directoryItemFacadesByPath[currentDir] = current = new DirectoryItemFacade
                            {
                                Name = currentDir,
                                Files = new List<IComponentStream>(),
                                Directories = new List<DirectoryItemFacade>(),
                            };
                        }

                        // If we came from a directory, we add it to our graph.
                        if (last != null)
                        {
                            current.Directories.Add(last);
                        }

                        // If we didn't come from a directory, it's because we're just getting started. Our current directory should include the file that led to it showing up in the graph.
                        else
                        {
                            current.Files.Add(item);
                        }

                        if (last != null && current.Files.FirstOrDefault(x => string.Equals(Path.GetFileName(x.Location), "pom.xml", StringComparison.OrdinalIgnoreCase)) != null)
                        {
                            this.Logger.LogDebug("Ignoring pom.xml at {ChildPomXmlLocation}, as it has a parent pom.xml that will be processed at {ParentDirName}\\pom.xml .", item.Location, current.Name);
                            break;
                        }

                        last = current;
                    }

                    // Go all the way up
                    while (currentDir != null);
                },
                s.OnCompleted);
        });
    }
}

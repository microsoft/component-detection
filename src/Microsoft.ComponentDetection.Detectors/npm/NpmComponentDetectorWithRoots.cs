namespace Microsoft.ComponentDetection.Detectors.Npm;
using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Export(typeof(IComponentDetector))]
public class NpmComponentDetectorWithRoots : FileComponentDetector
{
    private const string NpmRegistryHost = "registry.npmjs.org";

    private readonly object lernaFilesLock = new object();

    public const string LernaSearchPattern = "lerna.json";

    /// <summary>Common delegate for Package.json JToken processing.</summary>
    /// <param name="token">A JToken, usually corresponding to a package.json file.</param>
    /// <returns>Used in scenarios where one file path creates multiple JTokens, a false value indicates processing additional JTokens should be halted, proceed otherwise.</returns>
    protected delegate bool JTokenProcessor(JToken token);

    /// <summary>Gets or sets the logger for writing basic logging message to both console and file. Injected automatically by MEF composition.</summary>
    [Import]
    public IPathUtilityService PathUtilityService { get; set; }

    public override string Id { get; } = "NpmWithRoots";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "package-lock.json", "npm-shrinkwrap.json", LernaSearchPattern };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

    public override int Version { get; } = 2;

    public ICollection<ProcessRequest> LernaFiles { get; } = new List<ProcessRequest>();

    /// <inheritdoc />
    protected override IList<string> SkippedFolders => new List<string> { "node_modules", "pnpm-store" };

    private static void EnqueueDependencies(Queue<(JProperty Dependency, TypedComponent ParentComponent)> queue, JToken dependencies, TypedComponent parentComponent)
    {
        if (dependencies != null)
        {
            foreach (var dependency in dependencies.Cast<JProperty>())
            {
                if (dependency != null)
                {
                    queue.Enqueue((dependency, parentComponent));
                }
            }
        }
    }

    private static bool TryEnqueueFirstLevelDependencies(Queue<(JProperty DependencyProperty, TypedComponent ParentComponent)> queue, JToken dependencies, IDictionary<string, JProperty> dependencyLookup, Queue<TypedComponent> parentComponentQueue = null, TypedComponent parentComponent = null, bool skipValidation = false)
    {
        var isValid = true;
        if (dependencies != null)
        {
            foreach (var dependency in dependencies.Cast<JProperty>())
            {
                if (dependency == null || dependency.Name == null)
                {
                    continue;
                }

                var inLock = dependencyLookup.TryGetValue(dependency.Name, out var dependencyProperty);
                if (inLock)
                {
                    queue.Enqueue((dependencyProperty, parentComponent));
                }
                else if (skipValidation)
                {
                    continue;
                }
                else
                {
                    isValid = false;
                }
            }
        }

        return isValid;
    }

    protected override Task<IObservable<ProcessRequest>> OnPrepareDetection(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs) => Task.FromResult(this.RemoveNodeModuleNestedFiles(processRequests)
            .Where(pr =>
            {
                if (pr.ComponentStream.Pattern.Equals(LernaSearchPattern, StringComparison.Ordinal))
                {
                    // Lock LernaFiles so we don't add while we are enumerating in processFiles
                    lock (this.lernaFilesLock)
                    {
                        this.LernaFiles.Add(pr);
                        return false;
                    }
                }

                return true;
            }));

    protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        if (processRequest is null)
        {
            throw new ArgumentNullException(nameof(processRequest));
        }

        IEnumerable<string> packageJsonPattern = new List<string> { "package.json" };
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var packageJsonComponentStream = this.ComponentStreamEnumerableFactory.GetComponentStreams(new FileInfo(file.Location).Directory, packageJsonPattern, (name, directoryName) => false, false);

        var foundUnderLerna = false;
        IList<ProcessRequest> lernaFilesClone;

        // ToList LernaFiles to generate a copy we can act on without holding the lock for a long time
        lock (this.lernaFilesLock)
        {
            lernaFilesClone = this.LernaFiles.ToList();
        }

        foreach (var lernaProcessRequest in lernaFilesClone)
        {
            var lernaFile = lernaProcessRequest.ComponentStream;

            // We have extra validation on lock files not found below a lerna.json
            if (this.PathUtilityService.IsFileBelowAnother(lernaFile.Location, file.Location))
            {
                foundUnderLerna = true;
                break;
            }
        }

        await this.SafeProcessAllPackageJTokens(file, (token) =>
        {
            if (!foundUnderLerna && (token["name"] == null || token["version"] == null || string.IsNullOrWhiteSpace(token["name"].Value<string>()) || string.IsNullOrWhiteSpace(token["version"].Value<string>())))
            {
                this.Logger.LogInfo($"{file.Location} does not contain a valid name and/or version. These are required fields for a valid package-lock.json file." +
                                    $"It and its dependencies will not be registered.");
                return false;
            }

            this.ProcessIndividualPackageJTokens(singleFileComponentRecorder, token, packageJsonComponentStream, skipValidation: foundUnderLerna);
            return true;
        }).ConfigureAwait(true);
    }

    protected Task ProcessAllPackageJTokensAsync(IComponentStream componentStream, JTokenProcessor jtokenProcessor)
    {
        if (componentStream is null)
        {
            throw new ArgumentNullException(nameof(componentStream));
        }

        if (jtokenProcessor is null)
        {
            throw new ArgumentNullException(nameof(jtokenProcessor));
        }

        try
        {
            if (!componentStream.Stream.CanRead)
            {
                componentStream.Stream.ReadByte();
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogInfo($"Could not read {componentStream.Location} file.");
            this.Logger.LogFailedReadingFile(componentStream.Location, ex);
            return Task.CompletedTask;
        }

        using var file = new StreamReader(componentStream.Stream);
        using var reader = new JsonTextReader(file);

        var o = JToken.ReadFrom(reader);
        jtokenProcessor(o);
        return Task.CompletedTask;
    }

    protected void ProcessIndividualPackageJTokens(ISingleFileComponentRecorder singleFileComponentRecorder, JToken packageLockJToken, IEnumerable<IComponentStream> packageJsonComponentStream, bool skipValidation = false)
    {
        if (singleFileComponentRecorder is null)
        {
            throw new ArgumentNullException(nameof(singleFileComponentRecorder));
        }

        if (packageLockJToken is null)
        {
            throw new ArgumentNullException(nameof(packageLockJToken));
        }

        if (packageJsonComponentStream is null)
        {
            throw new ArgumentNullException(nameof(packageJsonComponentStream));
        }

        var dependencies = packageLockJToken["dependencies"];
        var topLevelDependencies = new Queue<(JProperty, TypedComponent)>();

        var dependencyLookup = dependencies.Children<JProperty>().ToDictionary(dependency => dependency.Name);

        foreach (var stream in packageJsonComponentStream)
        {
            using var file = new StreamReader(stream.Stream);
            using var reader = new JsonTextReader(file);

            var packageJsonToken = JToken.ReadFrom(reader);
            var enqueued = TryEnqueueFirstLevelDependencies(topLevelDependencies, packageJsonToken["dependencies"], dependencyLookup, skipValidation: skipValidation);
            enqueued = enqueued && TryEnqueueFirstLevelDependencies(topLevelDependencies, packageJsonToken["devDependencies"], dependencyLookup, skipValidation: skipValidation);
            if (!enqueued)
            {
                // This represents a mismatch between lock file and package.json, break out and do not register anything for these files
                throw new InvalidOperationException($"InvalidPackageJson -- There was a mismatch between the components in the package.json and the lock file at '{singleFileComponentRecorder.ManifestFileLocation}'");
            }
        }

        if (!packageJsonComponentStream.Any())
        {
            throw new InvalidOperationException($"InvalidPackageJson -- There must be a package.json file at '{singleFileComponentRecorder.ManifestFileLocation}' for components to be registered");
        }

        this.TraverseRequirementAndDependencyTree(topLevelDependencies, dependencyLookup, singleFileComponentRecorder);
    }

    private IObservable<ProcessRequest> RemoveNodeModuleNestedFiles(IObservable<ProcessRequest> componentStreams)
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
                        currentDir = this.PathUtilityService.GetParentDirectory(currentDir);

                        // We've reached the top / root
                        if (currentDir == null)
                        {
                            // If our last directory isn't in our list of top level nodes, it should be added. This happens for the first processed item and then subsequent times we have a new root (edge cases with multiple hard drives, for example)
                            if (!directoryItemFacades.Contains(last))
                            {
                                directoryItemFacades.Add(last);
                            }

                            var skippedFolder = this.SkippedFolders.FirstOrDefault(folder => item.Location.Contains(folder, StringComparison.Ordinal));

                            // When node_modules is part of the path down to a given item, we skip the item. Otherwise, we yield the item.
                            if (string.IsNullOrEmpty(skippedFolder))
                            {
                                s.OnNext(processRequest);
                            }
                            else
                            {
                                this.Logger.LogVerbose($"Ignoring package-lock.json at {item.Location}, as it is inside a {skippedFolder} folder.");
                            }

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

                        last = current;
                    }

                    // Go all the way up
                    while (currentDir != null);
                },
                s.OnCompleted);
        });
    }

    private async Task SafeProcessAllPackageJTokens(IComponentStream componentStream, JTokenProcessor jtokenProcessor)
    {
        try
        {
            await this.ProcessAllPackageJTokensAsync(componentStream, jtokenProcessor).ConfigureAwait(true);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.Logger.LogInfo($"Could not parse Jtokens from {componentStream.Location} file.");
            this.Logger.LogFailedReadingFile(componentStream.Location, e);
            return;
        }
    }

    private void TraverseRequirementAndDependencyTree(IEnumerable<(JProperty Dependency, TypedComponent ParentComponent)> topLevelDependencies, IDictionary<string, JProperty> dependencyLookup, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        // iterate through everything for a top level dependency with a single explicitReferencedDependency value
        foreach (var (currentDependency, _) in topLevelDependencies)
        {
            var typedComponent = NpmComponentUtilities.GetTypedComponent(currentDependency, NpmRegistryHost, this.Logger);
            if (typedComponent == null)
            {
                continue;
            }

            var previouslyAddedComponents = new HashSet<string> { typedComponent.Id };
            var subQueue = new Queue<(JProperty, TypedComponent)>();

            NpmComponentUtilities.TraverseAndRecordComponents(currentDependency, singleFileComponentRecorder, typedComponent, explicitReferencedDependency: typedComponent);
            EnqueueDependencies(subQueue, currentDependency.Value["dependencies"], parentComponent: typedComponent);
            TryEnqueueFirstLevelDependencies(subQueue, currentDependency.Value["requires"], dependencyLookup, parentComponent: typedComponent);

            while (subQueue.Count != 0)
            {
                var (currentSubDependency, parentComponent) = subQueue.Dequeue();

                var typedSubComponent = NpmComponentUtilities.GetTypedComponent(currentSubDependency, NpmRegistryHost, this.Logger);

                // only process components that we haven't seen before that have been brought in by the same explicitReferencedDependency, resolves circular npm 'requires' loop
                if (typedSubComponent == null || previouslyAddedComponents.Contains(typedSubComponent.Id))
                {
                    continue;
                }

                previouslyAddedComponents.Add(typedSubComponent.Id);

                NpmComponentUtilities.TraverseAndRecordComponents(currentSubDependency, singleFileComponentRecorder, typedSubComponent, explicitReferencedDependency: typedComponent, parentComponent.Id);

                EnqueueDependencies(subQueue, currentSubDependency.Value["dependencies"], parentComponent: typedSubComponent);
                TryEnqueueFirstLevelDependencies(subQueue, currentSubDependency.Value["requires"], dependencyLookup, parentComponent: typedSubComponent);
            }
        }
    }
}

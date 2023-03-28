namespace Microsoft.ComponentDetection.Detectors.Npm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class NpmComponentDetectorWithRoots : FileComponentDetector
{
    private const string NpmRegistryHost = "registry.npmjs.org";

    private readonly object lernaFilesLock = new object();

    public const string LernaSearchPattern = "lerna.json";

    /// <summary>
    /// Gets or sets the logger for writing basic logging message to both console and file.
    /// </summary>
    private readonly IPathUtilityService pathUtilityService;

    public NpmComponentDetectorWithRoots(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPathUtilityService pathUtilityService,
        ILogger<NpmComponentDetectorWithRoots> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.pathUtilityService = pathUtilityService;
        this.Logger = logger;
    }

    public NpmComponentDetectorWithRoots(IPathUtilityService pathUtilityService) => this.pathUtilityService = pathUtilityService;

    /// <summary>Common delegate for Package.json JToken processing.</summary>
    /// <param name="token">A JToken, usually corresponding to a package.json file.</param>
    /// <returns>Used in scenarios where one file path creates multiple JTokens, a false value indicates processing additional JTokens should be halted, proceed otherwise.</returns>
    protected delegate bool JTokenProcessingDelegate(JToken token);

    public override string Id { get; } = "NpmWithRoots";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "package-lock.json", "npm-shrinkwrap.json", LernaSearchPattern };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

    public override int Version { get; } = 2;

    public List<ProcessRequest> LernaFiles { get; set; } = new List<ProcessRequest>();

    /// <inheritdoc />
    protected override IList<string> SkippedFolders => new List<string> { "node_modules", "pnpm-store" };

    private static string NodeModules => NpmComponentUtilities.NodeModules;

    protected override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
    {
        return Task.FromResult(this.RemoveNodeModuleNestedFiles(processRequests)
            .Where(pr =>
            {
                if (pr.ComponentStream.Pattern.Equals(LernaSearchPattern))
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
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
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
            if (this.pathUtilityService.IsFileBelowAnother(lernaFile.Location, file.Location))
            {
                foundUnderLerna = true;
                break;
            }
        }

        await this.SafeProcessAllPackageJTokensAsync(file, (token) =>
        {
            if (!foundUnderLerna && (token["name"] == null || token["version"] == null || string.IsNullOrWhiteSpace(token["name"].Value<string>()) || string.IsNullOrWhiteSpace(token["version"].Value<string>())))
            {
                this.Logger.LogInformation("{PackageLogJsonFile} does not contain a valid name and/or version. These are required fields for a valid package-lock.json file. It and its dependencies will not be registered.", file.Location);
                return false;
            }

            this.ProcessIndividualPackageJTokens(singleFileComponentRecorder, token, packageJsonComponentStream, skipValidation: foundUnderLerna);
            return true;
        });
    }

    protected Task ProcessAllPackageJTokensAsync(IComponentStream componentStream, JTokenProcessingDelegate jtokenProcessor)
    {
        try
        {
            if (!componentStream.Stream.CanRead)
            {
                componentStream.Stream.ReadByte();
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogInformation(ex, "Could not read {ComponentStreamFile} file.", componentStream.Location);
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
        var lockFileVersion = packageLockJToken.Value<int>("lockfileVersion");

        var dependencies = lockFileVersion == 3 ? packageLockJToken["packages"] : packageLockJToken["dependencies"];

        var topLevelDependencies = new Queue<(JProperty, TypedComponent)>();

        var dependencyLookup = dependencies.Children<JProperty>().ToDictionary(dependency => dependency.Name);

        foreach (var stream in packageJsonComponentStream)
        {
            using var file = new StreamReader(stream.Stream);
            using var reader = new JsonTextReader(file);

            var packageJsonToken = JToken.ReadFrom(reader);
            var enqueued = this.TryEnqueueFirstLevelDependencies(topLevelDependencies, packageJsonToken["dependencies"], dependencyLookup, lockFileVersion: lockFileVersion, skipValidation: skipValidation);
            enqueued = enqueued && this.TryEnqueueFirstLevelDependencies(topLevelDependencies, packageJsonToken["devDependencies"], dependencyLookup, lockFileVersion: lockFileVersion, skipValidation: skipValidation);
            if (!enqueued)
            {
                // This represents a mismatch between lock file and package.json, break out and do not register anything for these files
                throw new InvalidOperationException(string.Format("InvalidPackageJson -- There was a mismatch between the components in the package.json and the lock file at '{0}'", singleFileComponentRecorder.ManifestFileLocation));
            }
        }

        if (!packageJsonComponentStream.Any())
        {
            throw new InvalidOperationException(string.Format("InvalidPackageJson -- There must be a package.json file at '{0}' for components to be registered", singleFileComponentRecorder.ManifestFileLocation));
        }

        this.TraverseRequirementAndDependencyTree(topLevelDependencies, dependencyLookup, lockFileVersion, singleFileComponentRecorder);
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
                        currentDir = this.pathUtilityService.GetParentDirectory(currentDir);

                        // We've reached the top / root
                        if (currentDir == null)
                        {
                            // If our last directory isn't in our list of top level nodes, it should be added. This happens for the first processed item and then subsequent times we have a new root (edge cases with multiple hard drives, for example)
                            if (!directoryItemFacades.Contains(last))
                            {
                                directoryItemFacades.Add(last);
                            }

                            var skippedFolder = this.SkippedFolders.FirstOrDefault(folder => item.Location.Contains(folder));

                            // When node_modules is part of the path down to a given item, we skip the item. Otherwise, we yield the item.
                            if (string.IsNullOrEmpty(skippedFolder))
                            {
                                s.OnNext(processRequest);
                            }
                            else
                            {
                                this.Logger.LogDebug("Ignoring package-lock.json at {PackageLockJsonLocation}, as it is inside a {SkippedFolder} folder.", item.Location, skippedFolder);
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

    private async Task SafeProcessAllPackageJTokensAsync(IComponentStream componentStream, JTokenProcessingDelegate jtokenProcessor)
    {
        try
        {
            await this.ProcessAllPackageJTokensAsync(componentStream, jtokenProcessor);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.Logger.LogInformation(e, "Could not parse Jtokens from {ComponentLocation} file.", componentStream.Location);
        }
    }

    private void TraverseRequirementAndDependencyTree(
        IEnumerable<(JProperty Dependency, TypedComponent ParentComponent)> topLevelDependencies,
        IDictionary<string, JProperty> dependencyLookup,
        int lockFileVersion,
        ISingleFileComponentRecorder singleFileComponentRecorder)
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

            if (lockFileVersion == 3)
            {
                this.TryEnqueueFirstLevelDependenciesLockfile3(subQueue, currentDependency.Value["dependencies"], dependencyLookup, singleFileComponentRecorder, parentComponent: typedComponent);
            }
            else
            {
                this.EnqueueDependencies(subQueue, currentDependency.Value["dependencies"], parentComponent: typedComponent);
                this.TryEnqueueFirstLevelDependencies(subQueue, currentDependency.Value["requires"], dependencyLookup, parentComponent: typedComponent);
            }

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

                if (lockFileVersion == 3)
                {
                    this.TryEnqueueFirstLevelDependenciesLockfile3(subQueue, currentSubDependency.Value["dependencies"], dependencyLookup, singleFileComponentRecorder, parentComponent: typedSubComponent);
                }
                else
                {
                    this.EnqueueDependencies(subQueue, currentSubDependency.Value["dependencies"], parentComponent: typedSubComponent);
                    this.TryEnqueueFirstLevelDependencies(subQueue, currentSubDependency.Value["requires"], dependencyLookup, parentComponent: typedSubComponent);
                }
            }
        }
    }

    private void EnqueueDependencies(Queue<(JProperty Dependency, TypedComponent ParentComponent)> queue, JToken dependencies, TypedComponent parentComponent)
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

    private bool TryEnqueueFirstLevelDependencies(
        Queue<(JProperty DependencyProperty, TypedComponent ParentComponent)> queue,
        JToken dependencies,
        IDictionary<string, JProperty> dependencyLookup,
        int lockFileVersion = 2,
        Queue<TypedComponent> parentComponentQueue = null,
        TypedComponent parentComponent = null,
        bool skipValidation = false)
    {
        if (dependencies == null)
        {
            return true;
        }

        var isValid = true;

        foreach (var dependency in dependencies.Cast<JProperty>())
        {
            if (dependency?.Name == null)
            {
                continue;
            }

            var inLock = dependencyLookup.TryGetValue(lockFileVersion == 3 ? $"{NodeModules}/{dependency.Name}" : dependency.Name, out var dependencyProperty);
            if (inLock)
            {
                queue.Enqueue((dependencyProperty, parentComponent));
            }
            else if (skipValidation)
            {
            }
            else
            {
                isValid = false;
            }
        }

        return isValid;
    }

    private bool TryEnqueueFirstLevelDependenciesLockfile3(
        Queue<(JProperty DependencyProperty, TypedComponent ParentComponent)> queue,
        JToken dependencies,
        IDictionary<string, JProperty> dependencyLookup,
        ISingleFileComponentRecorder componentRecorder,
        TypedComponent parentComponent)
    {
        if (dependencies == null)
        {
            return true;
        }

        var isValid = true;

        foreach (var dependency in dependencies.Cast<JProperty>())
        {
            if (dependency?.Name == null)
            {
                continue;
            }

            // First, check if there is an entry in the lockfile for this dependency nested in its ancestors
            var ancestors = componentRecorder.DependencyGraph.GetAncestors(parentComponent.Id);

            var inLock = false;
            JProperty dependencyProperty;
            ancestors.Add(parentComponent.Id);

            // Remove version information (eg "package 1.0.0 - npm" -> package)
            ancestors = ancestors.Select(x => x.Split(' ')[0]).ToList();

            // Search for the dependency in a depth-first manner nested in the ancestors
            var possibleDepPaths = ancestors.Select((t, i) => ancestors.TakeLast(ancestors.Count - i));

            foreach (var possibleDepPath in possibleDepPaths)
            {
                var ancestorNodeModulesPath =
                    $"{NodeModules}/{string.Join($"/{NodeModules}/", possibleDepPath)}/{NodeModules}/{dependency.Name}";

                // Does this exist?
                inLock = dependencyLookup.TryGetValue(ancestorNodeModulesPath, out dependencyProperty);

                if (!inLock)
                {
                    continue;
                }

                this.Logger.LogDebug("Found nested dependency {Dependency} in {AncestorNodeModulesPath}", dependency.Name, ancestorNodeModulesPath);
                queue.Enqueue((dependencyProperty, parentComponent));
                break;
            }

            if (inLock)
            {
                continue;
            }

            // If not, check if there is an entry in the lockfile for this dependency at the top level
            inLock = dependencyLookup.TryGetValue($"{NodeModules}/{dependency.Name}", out dependencyProperty);
            if (inLock)
            {
                queue.Enqueue((dependencyProperty, parentComponent));
            }
            else
            {
                isValid = false;
            }
        }

        return isValid;
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using global::DotNet.Globbing;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.Extensions.Logging;

public class YarnLockComponentDetector : FileComponentDetector
{
    private readonly IYarnLockFileFactory yarnLockFileFactory;

    public YarnLockComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IYarnLockFileFactory yarnLockFileFactory,
        ILogger<YarnLockComponentDetector> logger)
    {
        this.yarnLockFileFactory = yarnLockFileFactory;
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "Yarn";

    public override IList<string> SearchPatterns { get; } = ["yarn.lock"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Npm];

    public override int Version => 9;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Npm)];

    /// <inheritdoc />
    /// <remarks>"Package" is a more common substring, enclose it with \ to verify it is a folder.</remarks>
    protected override IList<string> SkippedFolders => ["node_modules", "pnpm-store", "\\package\\"];

    /// <inheritdoc />
    protected override bool EnableParallelism { get; set; } = true;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var skippedFolder = this.SkippedFolders.FirstOrDefault(folder => file.Location.Contains(folder));
        if (!string.IsNullOrEmpty(skippedFolder))
        {
            this.Logger.LogInformation("Yarn.Lock file {YarnLockLocation} was found in a {SkippedFolder} folder and will be skipped.", file.Location, skippedFolder);
            return;
        }

        this.Logger.LogInformation("Processing file {YarnLockLocation}", file.Location);

        try
        {
            var parsed = await this.yarnLockFileFactory.ParseYarnLockFileAsync(singleFileComponentRecorder, file.Stream, this.Logger);
            this.RecordLockfileVersion(parsed.LockfileVersion);
            this.DetectComponents(parsed, file.Location, singleFileComponentRecorder);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Could not read components from file {YarnLockLocation}.", file.Location);
        }
    }

    private void DetectComponents(YarnLockFile file, string location, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var yarnPackages = new Dictionary<string, YarnEntry>();

        // Iterate once and build our provider lookup for all possible yarn packages requests
        // Each entry can satisfy more than one request in a Yarn.Lock file
        // Example: npm@2.3.4 can satisfy the requests for npm@2, npm@2.3.4 and npm@^2.3.4, and each of these cases are
        // explicitly listed in the lockfile. So we have a dictionary entry for each one of those "npm@{version}" strings
        // to resolve back to the package in question
        foreach (var entry in file.Entries)
        {
            foreach (var satisfiedVersion in entry.Satisfied)
            {
                var key = $"{entry.Name}@{satisfiedVersion}";
                var addSuccessful = yarnPackages.TryAdd(key, entry);
                if (!addSuccessful)
                {
                    this.Logger.LogWarning("Found duplicate entry {Key} in {Location}", key, location);
                }
            }
        }

        if (yarnPackages.Count == 0 || !this.TryReadPeerPackageJsonRequestsAsYarnEntries(singleFileComponentRecorder, location, yarnPackages, out var yarnRoots))
        {
            return;
        }

        foreach (var dependency in yarnRoots)
        {
            var root = new DetectedComponent(new NpmComponent(dependency.Name, dependency.Version));

            if (!string.IsNullOrWhiteSpace(dependency.Location))
            {
                root.AddComponentFilePath(dependency.Location);
            }

            this.AddDetectedComponentToGraph(root, null, singleFileComponentRecorder, isRootComponent: true);
        }

        // It's important that all of the root dependencies get registered *before* we start processing any non-root
        // dependencies; otherwise, we would miss root dependency links for root dependencies that are also indirect
        // transitive dependencies.
        foreach (var dependency in yarnRoots)
        {
            this.ParseTreeWithAssignedRoot(dependency, yarnPackages, singleFileComponentRecorder);
        }

        // Catch straggler top level packages in the yarn.lock file that aren't in the package.lock file for whatever reason
        foreach (var entry in file.Entries)
        {
            var component = new DetectedComponent(new NpmComponent(entry.Name, entry.Version));
            if (singleFileComponentRecorder.GetComponent(component.Component.Id) == null)
            {
                this.AddDetectedComponentToGraph(component, parentComponent: null, singleFileComponentRecorder);
            }
        }
    }

    /// <summary>
    /// Takes a tree of components from a package.json root and adds/modifies detected components appropriately.
    /// </summary>
    /// <param name="root">The root of the section of the graph that we are parsing.</param>
    /// <param name="providerTable">A list of all possible yarn components to do lookups against.</param>
    /// <param name="singleFileComponentRecorder">The component recorder for file that is been processed.</param>
    private void ParseTreeWithAssignedRoot(YarnEntry root, Dictionary<string, YarnEntry> providerTable, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var queue = new Queue<(YarnEntry, YarnEntry)>();

        queue.Enqueue((root, null));
        var processed = new HashSet<string>();

        while (queue.Count > 0)
        {
            var (currentEntry, parentEntry) = queue.Dequeue();
            var currentComponent = singleFileComponentRecorder.GetComponent(this.YarnEntryToComponentId(currentEntry));
            var parentComponent = parentEntry != null ? singleFileComponentRecorder.GetComponent(this.YarnEntryToComponentId(parentEntry)) : null;

            if (currentComponent != null)
            {
                this.AddDetectedComponentToGraph(currentComponent, parentComponent, singleFileComponentRecorder, isDevDependency: root.DevDependency);
            }
            else
            {
                // If this is the first time we've seen a component...
                var detectedComponent = new DetectedComponent(new NpmComponent(currentEntry.Name, currentEntry.Version));
                this.AddDetectedComponentToGraph(detectedComponent, parentComponent, singleFileComponentRecorder, isDevDependency: root.DevDependency);
            }

            // Ensure that we continue to parse the tree for dependencies
            // also maintain a list of components we've seen in this set of the graph
            // so that we can short-circuit circular dependencies if we hit them
            var newDependencies = currentEntry.Dependencies.Concat(currentEntry.OptionalDependencies);
            foreach (var newDependency in newDependencies)
            {
                if (providerTable.ContainsKey(newDependency.LookupKey))
                {
                    var subDependency = providerTable[newDependency.LookupKey];

                    if (!processed.Contains(subDependency.LookupKey))
                    {
                        processed.Add(subDependency.LookupKey);
                        queue.Enqueue((subDependency, currentEntry));
                    }
                }
                else
                {
                    this.Logger.LogInformation("Failed to find resolved dependency for {YarnDependency}", newDependency.LookupKey);
                }
            }
        }
    }

    /// <summary>
    /// We use the yarn.lock's peer package.json file to determine what constitutes a "top-level" package
    /// This function reads those from the package.json so that they can later be used as the starting points
    /// in traversing the dependency graph.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder for file that is been processed.</param>
    /// <param name="location">The file location of the yarn.lock file.</param>
    /// <param name="yarnEntries">All the yarn entries that we know about.</param>
    /// <param name="yarnRoots">The output yarnRoots that we care about using as starting points.</param>
    /// <returns>False if no package.json file was found at location, otherwise it returns true. </returns>
    private bool TryReadPeerPackageJsonRequestsAsYarnEntries(ISingleFileComponentRecorder singleFileComponentRecorder, string location, Dictionary<string, YarnEntry> yarnEntries, out List<YarnEntry> yarnRoots)
    {
        yarnRoots = [];

        var pkgJsons = this.ComponentStreamEnumerableFactory.GetComponentStreams(new FileInfo(location).Directory, ["package.json"], (name, directoryName) => false, recursivelyScanDirectories: false);

        IDictionary<string, IDictionary<string, bool>> combinedDependencies = new Dictionary<string, IDictionary<string, bool>>();

        var pkgJsonCount = 0;

        IList<string> yarnWorkspaces = [];
        foreach (var pkgJson in pkgJsons)
        {
            combinedDependencies = NpmComponentUtilities.TryGetAllPackageJsonDependencies(pkgJson.Stream, out yarnWorkspaces);
            pkgJsonCount++;
        }

        if (pkgJsonCount != 1)
        {
            this.Logger.LogWarning("No package.json was found for file at {Location}. It will not be registered.", location);
            return false;
        }

        var workspaceDependencyVsLocationMap = new Dictionary<string, string>();
        if (yarnWorkspaces.Count > 0)
        {
            this.GetWorkspaceDependencies(yarnWorkspaces, new FileInfo(location).Directory, combinedDependencies, workspaceDependencyVsLocationMap);
        }

        // Convert all of the dependencies we retrieved from package.json
        // into the appropriate yarn package
        foreach (var dependency in combinedDependencies)
        {
            var name = dependency.Key;
            foreach (var version in dependency.Value)
            {
                var entryKey = $"{name}@npm:{version.Key}";
                if (!yarnEntries.ContainsKey(entryKey))
                {
                    this.Logger.LogWarning("A package was requested in the package.json file that was a peer of {Location} but was not contained in the lockfile. {Name} - {VersionKey}", location, name, version.Key);
                    singleFileComponentRecorder.RegisterPackageParseFailure($"{name} - {version.Key}");
                    continue;
                }

                var entry = yarnEntries[entryKey];

                entry.DevDependency = version.Value;

                yarnRoots.Add(entry);

                var locationMapDictonaryKey = this.GetLocationMapKey(name, version.Key);
                if (workspaceDependencyVsLocationMap.TryGetValue(locationMapDictonaryKey, out location))
                {
                    entry.Location = location;
                }
            }
        }

        return true;
    }

    private void GetWorkspaceDependencies(IList<string> yarnWorkspaces, DirectoryInfo root, IDictionary<string, IDictionary<string, bool>> dependencies, IDictionary<string, string> workspaceDependencyVsLocationMap)
    {
        var ignoreCase = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var globOptions = new GlobOptions()
        {
            Evaluation = new EvaluationOptions()
            {
                CaseInsensitive = ignoreCase,
            },
        };

        foreach (var workspacePattern in yarnWorkspaces)
        {
            var glob = Glob.Parse($"{root.FullName.Replace('\\', '/')}/{workspacePattern}/package.json", globOptions);

            var componentStreams = this.ComponentStreamEnumerableFactory.GetComponentStreams(root, (file) => glob.IsMatch(file.FullName.Replace('\\', '/')), null, true);

            foreach (var stream in componentStreams)
            {
                this.Logger.LogInformation("{ComponentLocation} found for workspace {WorkspacePattern}", stream.Location, workspacePattern);
                var combinedDependencies = NpmComponentUtilities.TryGetAllPackageJsonDependencies(stream.Stream, out _);

                foreach (var dependency in combinedDependencies)
                {
                    this.ProcessWorkspaceDependency(dependencies, dependency, workspaceDependencyVsLocationMap, stream.Location);
                }
            }
        }
    }

    private void ProcessWorkspaceDependency(IDictionary<string, IDictionary<string, bool>> dependencies, KeyValuePair<string, IDictionary<string, bool>> newDependency, IDictionary<string, string> workspaceDependencyVsLocationMap, string streamLocation)
    {
        try
        {
            if (!dependencies.TryGetValue(newDependency.Key, out var existingDependency))
            {
                dependencies.Add(newDependency.Key, newDependency.Value);
                foreach (var item in newDependency.Value)
                {
                    // Adding 'Package.json stream's location'(in which workspacedependency of Yarn.lock file was found) as location of respective WorkSpaceDependency.
                    this.AddLocationInfoToWorkspaceDependency(workspaceDependencyVsLocationMap, streamLocation, newDependency.Key, item.Key);
                }

                return;
            }

            foreach (var item in newDependency.Value)
            {
                if (existingDependency.TryGetValue(item.Key, out var wasDev))
                {
                    existingDependency[item.Key] = wasDev && item.Value;
                }
                else
                {
                    existingDependency[item.Key] = item.Value;
                }

                // Adding 'Package.json stream's location'(in which workspacedependency of Yarn.lock file was found) as location of respective WorkSpaceDependency.
                this.AddLocationInfoToWorkspaceDependency(workspaceDependencyVsLocationMap, streamLocation, newDependency.Key, item.Key);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Could not process workspace dependency from file {PackageJsonStreamLocation}.", streamLocation);
        }
    }

    private void AddLocationInfoToWorkspaceDependency(IDictionary<string, string> workspaceDependencyVsLocationMap, string streamLocation, string dependencyName, string dependencyVersion)
    {
        var locationMapDictionaryKey = this.GetLocationMapKey(dependencyName, dependencyVersion);
        workspaceDependencyVsLocationMap.TryAdd(locationMapDictionaryKey, streamLocation);
    }

    private string GetLocationMapKey(string dependencyName, string dependencyVersion)
    {
        return $"{dependencyName}-{dependencyVersion}";
    }

    private void AddDetectedComponentToGraph(DetectedComponent componentToAdd, DetectedComponent parentComponent, ISingleFileComponentRecorder singleFileComponentRecorder, bool isRootComponent = false, bool? isDevDependency = null)
    {
        if (parentComponent == null)
        {
            singleFileComponentRecorder.RegisterUsage(componentToAdd, isRootComponent, isDevelopmentDependency: isDevDependency);
        }
        else
        {
            singleFileComponentRecorder.RegisterUsage(componentToAdd, isRootComponent, parentComponent.Component.Id, isDevelopmentDependency: isDevDependency);
        }
    }

    private string YarnEntryToComponentId(YarnEntry entry)
    {
        return new DetectedComponent(new NpmComponent(entry.Name, entry.Version)).Component.Id;
    }
}

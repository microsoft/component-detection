namespace Microsoft.ComponentDetection.Detectors.Npm;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.Extensions.Logging;

public class NpmLockfile3Detector : NpmLockfileDetectorBase
{
    private static readonly string NodeModules = NpmComponentUtilities.NodeModules;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public NpmLockfile3Detector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPathUtilityService pathUtilityService,
        ILogger<NpmLockfile3Detector> logger)
        : base(
            componentStreamEnumerableFactory,
            walkerFactory,
            pathUtilityService,
            logger)
    {
    }

    public NpmLockfile3Detector(IPathUtilityService pathUtilityService)
        : base(pathUtilityService)
    {
    }

    public override string Id => "NpmLockfile3";

    public override int Version => 2;

    protected override bool IsSupportedLockfileVersion(int lockfileVersion) => lockfileVersion == 3;

    protected override void ProcessLockfile(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        PackageJson packageJson,
        JsonDocument lockfile,
        int lockfileVersion)
    {
        var root = lockfile.RootElement;

        // Get packages from lockfile (v3 uses "packages" instead of "dependencies")
        if (!root.TryGetProperty("packages", out var packagesElement))
        {
            return;
        }

        // Build package lookup - keys are paths like "node_modules/lodash" or "node_modules/a/node_modules/b"
        var packageLookup = new Dictionary<string, (string Path, PackageLockV3Package Package)>();
        foreach (var pkg in packagesElement.EnumerateObject())
        {
            if (string.IsNullOrEmpty(pkg.Name))
            {
                continue; // Skip the root package (empty key)
            }

            var package = JsonSerializer.Deserialize<PackageLockV3Package>(pkg.Value.GetRawText(), JsonOptions);
            if (package is not null)
            {
                packageLookup[pkg.Name] = (pkg.Name, package);
            }
        }

        // Collect all top-level dependencies from package.json
        var topLevelDependencies = new Queue<(string Path, PackageLockV3Package Package, TypedComponent? Parent)>();

        this.EnqueueDependencies(topLevelDependencies, packageJson.Dependencies, packageLookup, null);
        this.EnqueueDependencies(topLevelDependencies, packageJson.DevDependencies, packageLookup, null);
        this.EnqueueDependencies(topLevelDependencies, packageJson.OptionalDependencies, packageLookup, null);

        // Process each top-level dependency
        while (topLevelDependencies.Count > 0)
        {
            var (path, lockPackage, _) = topLevelDependencies.Dequeue();
            var name = NpmComponentUtilities.GetModuleName(path);

            var component = this.CreateComponent(name, lockPackage.Version, lockPackage.Integrity);
            if (component is null)
            {
                continue;
            }

            var previouslyAddedComponents = new HashSet<string> { component.Id };
            var subQueue = new Queue<(string Path, PackageLockV3Package Package, TypedComponent Parent)>();

            // Record the top-level component
            this.RecordComponent(singleFileComponentRecorder, component, lockPackage.Dev ?? false, component);

            // Enqueue nested dependencies
            this.EnqueueNestedDependencies(subQueue, path, lockPackage, packageLookup, singleFileComponentRecorder, component);

            // Process sub-dependencies
            while (subQueue.Count > 0)
            {
                var (subPath, subPackage, parentComponent) = subQueue.Dequeue();
                var subName = NpmComponentUtilities.GetModuleName(subPath);

                var subComponent = this.CreateComponent(subName, subPackage.Version, subPackage.Integrity);
                if (subComponent is null || previouslyAddedComponents.Contains(subComponent.Id))
                {
                    continue;
                }

                previouslyAddedComponents.Add(subComponent.Id);

                this.RecordComponent(singleFileComponentRecorder, subComponent, subPackage.Dev ?? false, component, parentComponent.Id);

                this.EnqueueNestedDependencies(subQueue, subPath, subPackage, packageLookup, singleFileComponentRecorder, subComponent);
            }
        }
    }

    private void EnqueueDependencies(
        Queue<(string Path, PackageLockV3Package Package, TypedComponent? Parent)> queue,
        IDictionary<string, string>? dependencies,
        Dictionary<string, (string Path, PackageLockV3Package Package)> packageLookup,
        TypedComponent? parent)
    {
        if (dependencies is null)
        {
            return;
        }

        foreach (var dep in dependencies)
        {
            var path = $"{NodeModules}/{dep.Key}";
            if (packageLookup.TryGetValue(path, out var lockPkg))
            {
                queue.Enqueue((lockPkg.Path, lockPkg.Package, parent));
            }
        }
    }

    private void EnqueueNestedDependencies(
        Queue<(string Path, PackageLockV3Package Package, TypedComponent Parent)> queue,
        string currentPath,
        PackageLockV3Package package,
        Dictionary<string, (string Path, PackageLockV3Package Package)> packageLookup,
        ISingleFileComponentRecorder componentRecorder,
        TypedComponent parent)
    {
        if (package.Dependencies is null)
        {
            return;
        }

        foreach (var dep in package.Dependencies)
        {
            // First, check if there is an entry in the lockfile for this dependency nested in its ancestors
            var ancestors = componentRecorder.DependencyGraph.GetAncestors(parent.Id);
            ancestors.Add(parent.Id);

            // Remove version information from ancestor IDs
            ancestors = ancestors.Select(x => x.Split(' ')[0]).ToList();

            var found = false;

            // Depth-first search through ancestors
            for (var i = 0; i < ancestors.Count && !found; i++)
            {
                var possiblePath = ancestors.Skip(i).ToList();
                var ancestorNodeModulesPath = string.Format(
                    "{0}/{1}/{0}/{2}",
                    NodeModules,
                    string.Join($"/{NodeModules}/", possiblePath),
                    dep.Key);

                if (packageLookup.TryGetValue(ancestorNodeModulesPath, out var nestedPkg))
                {
                    this.Logger.LogDebug("Found nested dependency {Dependency} in {AncestorNodeModulesPath}", dep.Key, ancestorNodeModulesPath);
                    queue.Enqueue((nestedPkg.Path, nestedPkg.Package, parent));
                    found = true;
                }
            }

            if (found)
            {
                continue;
            }

            // If not found in ancestors, check at the top level
            var topLevelPath = $"{NodeModules}/{dep.Key}";
            if (packageLookup.TryGetValue(topLevelPath, out var topLevelPkg))
            {
                queue.Enqueue((topLevelPkg.Path, topLevelPkg.Package, parent));
            }
            else
            {
                this.Logger.LogWarning("Could not find dependency {Dependency} in lockfile", dep.Key);
            }
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.Npm;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.Extensions.Logging;

public class NpmComponentDetectorWithRoots : NpmLockfileDetectorBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public NpmComponentDetectorWithRoots(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPathUtilityService pathUtilityService,
        ILogger<NpmComponentDetectorWithRoots> logger)
        : base(
            componentStreamEnumerableFactory,
            walkerFactory,
            pathUtilityService,
            logger)
    {
    }

    public NpmComponentDetectorWithRoots(IPathUtilityService pathUtilityService)
        : base(pathUtilityService)
    {
    }

    public override string Id => "NpmWithRoots";

    public override int Version => 3;

    protected override bool IsSupportedLockfileVersion(int lockfileVersion) => lockfileVersion != 3;

    protected override void ProcessLockfile(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        PackageJson packageJson,
        JsonDocument lockfile,
        int lockfileVersion)
    {
        var root = lockfile.RootElement;

        // Get dependencies from lockfile
        if (!root.TryGetProperty("dependencies", out var dependenciesElement))
        {
            return;
        }

        // Build dependency lookup
        var dependencyLookup = new Dictionary<string, (string Name, PackageLockV1Dependency Dependency)>();
        foreach (var dep in dependenciesElement.EnumerateObject())
        {
            var dependency = JsonSerializer.Deserialize<PackageLockV1Dependency>(dep.Value.GetRawText(), JsonOptions);
            if (dependency is not null)
            {
                dependencyLookup[dep.Name] = (dep.Name, dependency);
            }
        }

        // Collect all top-level dependencies from package.json
        var topLevelDependencies = new Queue<(string Name, PackageLockV1Dependency Dependency, TypedComponent? Parent)>();

        this.EnqueueDependencies(topLevelDependencies, packageJson.Dependencies, dependencyLookup, null);
        this.EnqueueDependencies(topLevelDependencies, packageJson.DevDependencies, dependencyLookup, null);
        this.EnqueueDependencies(topLevelDependencies, packageJson.OptionalDependencies, dependencyLookup, null);

        // Process each top-level dependency
        while (topLevelDependencies.Count > 0)
        {
            var (name, lockDependency, _) = topLevelDependencies.Dequeue();

            var component = this.CreateComponent(name, lockDependency.Version, lockDependency.Integrity);
            if (component is null)
            {
                continue;
            }

            var previouslyAddedComponents = new HashSet<string> { component.Id };
            var subQueue = new Queue<(string Name, PackageLockV1Dependency Dependency, TypedComponent Parent)>();

            // Record the top-level component
            this.RecordComponent(singleFileComponentRecorder, component, lockDependency.Dev ?? false, component);

            // Enqueue nested dependencies and requires
            this.EnqueueNestedDependencies(subQueue, lockDependency, dependencyLookup, component);

            // Process sub-dependencies
            while (subQueue.Count > 0)
            {
                var (subName, subDependency, parentComponent) = subQueue.Dequeue();

                var subComponent = this.CreateComponent(subName, subDependency.Version, subDependency.Integrity);
                if (subComponent is null || previouslyAddedComponents.Contains(subComponent.Id))
                {
                    continue;
                }

                previouslyAddedComponents.Add(subComponent.Id);

                this.RecordComponent(singleFileComponentRecorder, subComponent, subDependency.Dev ?? false, component, parentComponent.Id);

                this.EnqueueNestedDependencies(subQueue, subDependency, dependencyLookup, subComponent);
            }
        }
    }

    private void EnqueueDependencies(
        Queue<(string Name, PackageLockV1Dependency Dependency, TypedComponent? Parent)> queue,
        IDictionary<string, string>? dependencies,
        Dictionary<string, (string Name, PackageLockV1Dependency Dependency)> dependencyLookup,
        TypedComponent? parent)
    {
        if (dependencies is null)
        {
            return;
        }

        foreach (var (name, dependency) in dependencies.Keys
            .Where(dependencyLookup.ContainsKey)
            .Select(key => dependencyLookup[key]))
        {
            queue.Enqueue((name, dependency, parent));
        }
    }

    private void EnqueueNestedDependencies(
        Queue<(string Name, PackageLockV1Dependency Dependency, TypedComponent Parent)> queue,
        PackageLockV1Dependency dependency,
        Dictionary<string, (string Name, PackageLockV1Dependency Dependency)> dependencyLookup,
        TypedComponent parent)
    {
        // Enqueue nested dependencies (these are local to this package)
        if (dependency.Dependencies is not null)
        {
            foreach (var (name, nestedDep) in dependency.Dependencies)
            {
                queue.Enqueue((name, nestedDep, parent));
            }
        }

        // Enqueue requires (these reference the top-level lookup)
        if (dependency.Requires is not null)
        {
            foreach (var (name, dep) in dependency.Requires.Keys
                .Where(dependencyLookup.ContainsKey)
                .Select(key => dependencyLookup[key]))
            {
                queue.Enqueue((name, dep, parent));
            }
        }
    }
}

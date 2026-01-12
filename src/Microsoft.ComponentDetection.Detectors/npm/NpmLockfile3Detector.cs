namespace Microsoft.ComponentDetection.Detectors.Npm;

using System.Collections.Generic;
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

        // Collect direct dependencies from package.json for explicit reference tracking
        var directDependencies = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (packageJson.Dependencies is not null)
        {
            foreach (var dep in packageJson.Dependencies.Keys)
            {
                directDependencies.Add(dep);
            }
        }

        if (packageJson.DevDependencies is not null)
        {
            foreach (var dep in packageJson.DevDependencies.Keys)
            {
                directDependencies.Add(dep);
            }
        }

        if (packageJson.OptionalDependencies is not null)
        {
            foreach (var dep in packageJson.OptionalDependencies.Keys)
            {
                directDependencies.Add(dep);
            }
        }

        // Build package lookup and component map - keys are paths like "node_modules/lodash" or "node_modules/a/node_modules/b"
        var packageLookup = new Dictionary<string, (string Path, PackageLockV3Package Package)>();
        var componentMap = new Dictionary<string, TypedComponent>();
        var componentDevStatus = new Dictionary<string, bool>();

        // First pass: Create all components and determine dev status
        foreach (var pkg in packagesElement.EnumerateObject())
        {
            if (string.IsNullOrEmpty(pkg.Name))
            {
                continue; // Skip the root package (empty key)
            }

            var package = JsonSerializer.Deserialize<PackageLockV3Package>(pkg.Value.GetRawText(), JsonOptions);
            if (package is null)
            {
                continue;
            }

            // Skip link packages (symbolic links to workspace packages)
            if (package.Link == true)
            {
                continue;
            }

            // Skip bundled dependencies (they are installed by their parent)
            if (package.InBundle == true)
            {
                continue;
            }

            packageLookup[pkg.Name] = (pkg.Name, package);

            // Derive package name from path
            var name = NpmComponentUtilities.GetModuleName(pkg.Name);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(package.Version))
            {
                continue;
            }

            var component = this.CreateComponent(name, package.Version, package.Integrity);
            if (component is null)
            {
                continue;
            }

            // Check both Dev and DevOptional. In npm lockfiles, devOptional is set when a package has both peer: true and dev: true,
            // and for detection purposes we treat devOptional packages as dev dependencies.
            var isDevDependency = package.Dev == true || package.DevOptional == true;

            // Track component and its dev status
            // If a component appears multiple times (at different paths), it's dev-only if ALL instances are dev
            if (componentMap.TryGetValue(component.Id, out _))
            {
                // Already seen this component - update dev status (if any is non-dev, it's not dev-only)
                componentDevStatus[component.Id] = componentDevStatus[component.Id] && isDevDependency;
            }
            else
            {
                componentMap[component.Id] = component;
                componentDevStatus[component.Id] = isDevDependency;
            }
        }

        // Second pass: Register all components
        foreach (var (componentId, component) in componentMap)
        {
            var isDevDependency = componentDevStatus[componentId];

            // Check if this is a direct dependency from package.json
            var npmComponent = (NpmComponent)component;
            var isDirectDependency = directDependencies.Contains(npmComponent.Name);

            this.RecordComponent(singleFileComponentRecorder, component, isDevDependency, isDirectDependency);
        }

        // Third pass: Build dependency graph edges using node-style resolution
        foreach (var (path, (_, package)) in packageLookup)
        {
            if (package.Dependencies is null && package.OptionalDependencies is null)
            {
                continue;
            }

            var name = NpmComponentUtilities.GetModuleName(path);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(package.Version))
            {
                continue;
            }

            var parentComponent = this.CreateComponent(name, package.Version, package.Integrity);
            if (parentComponent is null || !componentMap.ContainsKey(parentComponent.Id))
            {
                continue;
            }

            // Process regular dependencies
            this.ProcessDependencyEdges(path, package.Dependencies, packageLookup, componentMap, singleFileComponentRecorder, parentComponent);

            // Process optional dependencies
            this.ProcessDependencyEdges(path, package.OptionalDependencies, packageLookup, componentMap, singleFileComponentRecorder, parentComponent);
        }
    }

    /// <summary>
    /// Resolves a dependency using node-style module resolution.
    /// Walks up from the current path checking for the dependency in nested node_modules folders.
    /// </summary>
    private string? ResolveDependencyPath(
        string fromPath,
        string dependencyName,
        Dictionary<string, (string Path, PackageLockV3Package Package)> packageLookup)
    {
        var basePath = fromPath;

        while (true)
        {
            // Build candidate path: either at top level or nested in current base
            var candidate = string.IsNullOrEmpty(basePath) || basePath == NodeModules
                ? $"{NodeModules}/{dependencyName}"
                : $"{basePath}/{NodeModules}/{dependencyName}";

            if (packageLookup.TryGetValue(candidate, out var pkg) && !string.IsNullOrEmpty(pkg.Package.Version))
            {
                return candidate;
            }

            // Move up to parent's node_modules
            if (string.IsNullOrEmpty(basePath))
            {
                return null;
            }

            basePath = this.GetParentPackagePath(basePath);
        }
    }

    /// <summary>
    /// Gets the parent package path by removing the trailing /node_modules/pkg segment.
    /// </summary>
    private string? GetParentPackagePath(string packagePath)
    {
        // "node_modules/a/node_modules/b" -> "node_modules/a"
        // "node_modules/@scope/a/node_modules/@scope/b" -> "node_modules/@scope/a"
        const string marker = "/node_modules/";
        var idx = packagePath.LastIndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var parent = packagePath[..idx];
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    /// <summary>
    /// Processes dependency edges for a package, resolving each dependency using node-style resolution.
    /// </summary>
    private void ProcessDependencyEdges(
        string fromPath,
        IDictionary<string, string>? dependencies,
        Dictionary<string, (string Path, PackageLockV3Package Package)> packageLookup,
        Dictionary<string, TypedComponent> componentMap,
        ISingleFileComponentRecorder componentRecorder,
        TypedComponent parentComponent)
    {
        if (dependencies is null)
        {
            return;
        }

        foreach (var dep in dependencies)
        {
            var resolvedPath = this.ResolveDependencyPath(fromPath, dep.Key, packageLookup);
            if (resolvedPath is null)
            {
                this.Logger.LogDebug("Could not resolve dependency {Dependency} from {FromPath}", dep.Key, fromPath);
                continue;
            }

            if (!packageLookup.TryGetValue(resolvedPath, out var resolvedPkg))
            {
                continue;
            }

            var resolvedName = NpmComponentUtilities.GetModuleName(resolvedPath);
            if (string.IsNullOrEmpty(resolvedName) || string.IsNullOrEmpty(resolvedPkg.Package.Version))
            {
                continue;
            }

            var childComponent = this.CreateComponent(resolvedName, resolvedPkg.Package.Version, resolvedPkg.Package.Integrity);
            if (childComponent is null || !componentMap.ContainsKey(childComponent.Id))
            {
                continue;
            }

            // Register the dependency edge
            componentRecorder.RegisterUsage(
                new DetectedComponent(childComponent),
                parentComponentId: parentComponent.Id);
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.Uv;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.Extensions.Logging;

public class UvLockComponentDetector : FileComponentDetector, IExperimentalDetector
{
    public UvLockComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<UvLockComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "UvLock";

    public override IList<string> SearchPatterns { get; } = ["uv.lock"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Pip, ComponentType.Git];

    public override int Version => 2;

    public override IEnumerable<string> Categories => ["Python"];

    internal static bool IsRootPackage(UvPackage pck)
    {
        return pck.Source?.Virtual != null;
    }

    internal static HashSet<string> GetTransitivePackages(IEnumerable<string> roots, List<UvPackage> packages)
    {
        // A package name can appear more than once in a uv.lock (e.g. when resolution
        // markers select different versions per platform). Group by name and union the
        // dependencies of every matching entry so traversal is resilient to duplicates.
        var lookup = packages
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(p => p.Dependencies.Select(d => d.Name)).ToList(),
                StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(roots);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!visited.Add(name))
            {
                continue;
            }

            if (lookup.TryGetValue(name, out var deps))
            {
                foreach (var dep in deps)
                {
                    queue.Enqueue(dep);
                }
            }
        }

        return visited;
    }

    /// <summary>
    /// Resolves the concrete package a dependency refers to. A package name can appear
    /// more than once in a uv.lock (e.g. when resolution markers select different versions
    /// per platform), so when multiple candidates share the name the dependency's version
    /// specifier is used to pick the matching package.
    /// </summary>
    /// <param name="dep">The dependency reference to resolve.</param>
    /// <param name="packages">All packages parsed from the uv.lock.</param>
    /// <returns>The matching package, or null when no package with the name exists.</returns>
    internal UvPackage? ResolveDependencyPackage(UvDependency dep, List<UvPackage> packages)
    {
        var candidates = packages
            .Where(p => p.Name.Equals(dep.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count <= 1)
        {
            return candidates.FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(dep.Specifier))
        {
            var specs = dep.Specifier.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var matching = candidates
                .Where(p =>
                {
                    try
                    {
                        return PythonVersionUtilities.VersionValidForSpec(p.Version, specs);
                    }
                    catch (ArgumentException)
                    {
                        this.Logger.LogWarning("Invalid version specifier {Specifier} for dependency {DependencyName}", dep.Specifier, dep.Name);
                        return false;
                    }
                })
                .OrderBy(p => p.Version, new PythonVersionComparer())
                .ToList();

            if (matching.Count > 0)
            {
                return matching[0];
            }
        }

        return candidates[0];
    }

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        try
        {
            // Parse the file stream into a UvLock model
            file.Stream.Position = 0; // Ensure stream is at the beginning
            var uvLock = UvLock.Parse(file.Stream);

            var rootPackage = uvLock.Packages.FirstOrDefault(IsRootPackage);
            var explicitPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var devRootNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (rootPackage != null)
            {
                foreach (var dep in rootPackage.MetadataRequiresDist)
                {
                    explicitPackages.Add(dep.Name);
                }

                foreach (var devDep in rootPackage.MetadataRequiresDev)
                {
                    devRootNames.Add(devDep.Name);
                }
            }

            // Compute dev-only packages via transitive reachability analysis.
            // A package is dev-only if it is reachable from dev roots but NOT from production roots.
            var prodRoots = rootPackage?.Dependencies.Select(d => d.Name) ?? [];
            var prodTransitive = GetTransitivePackages(prodRoots, uvLock.Packages);
            var devTransitive = GetTransitivePackages(devRootNames, uvLock.Packages);
            var devOnlyPackages = new HashSet<string>(devTransitive.Except(prodTransitive, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            foreach (var pkg in uvLock.Packages)
            {
                if (IsRootPackage(pkg))
                {
                    continue;
                }

                var isExplicit = explicitPackages.Contains(pkg.Name);
                var isDev = devOnlyPackages.Contains(pkg.Name);

                var component = pkg.ToTypedComponent();
                var detectedComponent = new DetectedComponent(component);
                singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: isDev, isExplicitReferencedDependency: isExplicit);

                foreach (var dep in pkg.Dependencies)
                {
                    var depPkg = this.ResolveDependencyPackage(dep, uvLock.Packages);
                    if (depPkg != null)
                    {
                        var depComponent = depPkg.ToTypedComponent();
                        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(depComponent), parentComponentId: component.Id, isDevelopmentDependency: isDev);
                    }
                    else
                    {
                        this.Logger.LogWarning("Dependency {DependencyName} not found in uv.lock packages", dep.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to parse uv.lock file {File}", file.Location);
        }

        return Task.CompletedTask;
    }
}

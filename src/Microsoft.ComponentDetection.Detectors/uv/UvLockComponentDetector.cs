namespace Microsoft.ComponentDetection.Detectors.Uv;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
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

    public override int Version => 1;

    public override IEnumerable<string> Categories => ["Python"];

    internal static bool IsRootPackage(UvPackage pck)
    {
        return pck.Source?.Virtual != null;
    }

    internal static (Uri RepositoryUrl, string CommitHash) ParseGitUrl(string gitUrl)
    {
        var uri = new Uri(gitUrl);
        var repoUrl = new Uri(uri.GetLeftPart(UriPartial.Path));
        var commitHash = uri.Fragment.TrimStart('#');
        return (repoUrl, commitHash);
    }

    internal static HashSet<string> GetTransitivePackages(IEnumerable<string> roots, List<UvPackage> packages)
    {
        var lookup = packages.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(roots);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!visited.Add(name))
            {
                continue;
            }

            if (lookup.TryGetValue(name, out var pkg))
            {
                foreach (var dep in pkg.Dependencies)
                {
                    queue.Enqueue(dep.Name);
                }
            }
        }

        return visited;
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
            var devOnlyPackages = new HashSet<string>(devTransitive.Except(prodTransitive), StringComparer.OrdinalIgnoreCase);

            foreach (var pkg in uvLock.Packages)
            {
                if (IsRootPackage(pkg))
                {
                    continue;
                }

                var isExplicit = explicitPackages.Contains(pkg.Name);
                var isDev = devOnlyPackages.Contains(pkg.Name);

                TypedComponent component;
                if (pkg.Source?.Git != null)
                {
                    var (repoUrl, commitHash) = ParseGitUrl(pkg.Source.Git);
                    component = new GitComponent(repoUrl, commitHash);
                }
                else
                {
                    component = new PipComponent(pkg.Name, pkg.Version);
                }

                var detectedComponent = new DetectedComponent(component);
                singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: isDev, isExplicitReferencedDependency: isExplicit);

                foreach (var dep in pkg.Dependencies)
                {
                    var depPkg = uvLock.Packages.FirstOrDefault(p => p.Name.Equals(dep.Name, StringComparison.OrdinalIgnoreCase));
                    if (depPkg != null)
                    {
                        TypedComponent depComponent;
                        if (depPkg.Source?.Git != null)
                        {
                            var (depRepoUrl, depCommitHash) = ParseGitUrl(depPkg.Source.Git);
                            depComponent = new GitComponent(depRepoUrl, depCommitHash);
                        }
                        else
                        {
                            depComponent = new PipComponent(depPkg.Name, depPkg.Version);
                        }
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

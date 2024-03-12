namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class PnpmComponentDetector : FileComponentDetector
{
    public PnpmComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<PnpmComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.NeedsAutomaticRootDependencyCalculation = true;
        this.Logger = logger;
    }

    public override string Id { get; } = "Pnpm";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "shrinkwrap.yaml", "pnpm-lock.yaml" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

    public override int Version { get; } = 6;

    /// <inheritdoc />
    protected override IList<string> SkippedFolders => new List<string> { "node_modules", "pnpm-store" };

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogDebug("Found yaml file: {YamlFile}", file.Location);
        var skippedFolder = this.SkippedFolders.FirstOrDefault(folder => file.Location.Contains(folder));
        if (!string.IsNullOrEmpty(skippedFolder))
        {
            this.Logger.LogDebug("Skipping found file, it was detected as being within a {SkippedFolder} folder.", skippedFolder);
        }

        try
        {
            var fileContent = await new StreamReader(file.Stream).ReadToEndAsync();
            var version = PnpmParsingUtilities.DeserializePnpmYamlFileVersion(fileContent);
            this.RecordLockfileVersion(version);
            var majorVersion = version.Split(".")[0];
            switch (majorVersion)
            {
                case null:
                // The null case falls through to version 5 to preserver the behavior of this scanner from before version specific logic was added.
                // This allows files explicitly versioned with shrinkwrapVersion (such as one included in some of the tests) to be used.
                // Given that "shrinkwrapVersion" is a concept from file format version 4 https://github.com/pnpm/spec/blob/master/lockfile/4.md)
                // this case might not be robust.
                case "5":
                    var pnpmYamlV5 = PnpmParsingUtilities.DeserializePnpmYamlV5File(fileContent);
                    this.RecordDependencyGraphFromFileV5(pnpmYamlV5, singleFileComponentRecorder);
                    break;
                case "6":
                    var pnpmYamlV6 = PnpmParsingUtilities.DeserializePnpmYamlV6File(fileContent);
                    this.RecordDependencyGraphFromFileV6(pnpmYamlV6, singleFileComponentRecorder);
                    break;
                default:
                    this.Logger.LogError("Unsupported lockfileVersion in pnpm yaml file {File}", file.Location);
                    break;
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read pnpm yaml file {File}", file.Location);
        }
    }

    private void RecordDependencyGraphFromFileV5(PnpmYamlV5 yaml, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        foreach (var packageKeyValue in yaml.packages ?? Enumerable.Empty<KeyValuePair<string, Package>>())
        {
            // Ignore file: as these are local packages.
            if (packageKeyValue.Key.StartsWith("file:"))
            {
                continue;
            }

            var parentDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV5(pnpmPackagePath: packageKeyValue.Key);
            var isDevDependency = packageKeyValue.Value != null && PnpmParsingUtilities.IsPnpmPackageDevDependency(packageKeyValue.Value);
            singleFileComponentRecorder.RegisterUsage(parentDetectedComponent, isDevelopmentDependency: isDevDependency);
            parentDetectedComponent = singleFileComponentRecorder.GetComponent(parentDetectedComponent.Component.Id);

            if (packageKeyValue.Value.dependencies != null)
            {
                foreach (var dependency in packageKeyValue.Value.dependencies)
                {
                    // Ignore local packages.
                    if (this.IsLocalDependency(dependency))
                    {
                        continue;
                    }

                    var childDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV5(pnpmPackagePath: this.CreatePnpmPackagePathFromDependencyV5(dependency.Key, dependency.Value));

                    // Older code used the root's dev dependency value. We're leaving this null until we do a second pass to look at each components' top level referrers.
                    singleFileComponentRecorder.RegisterUsage(childDetectedComponent, parentComponentId: parentDetectedComponent.Component.Id, isDevelopmentDependency: null);
                }
            }
        }

        // PNPM doesn't know at the time of RegisterUsage being called for a dependency whether something is a dev dependency or not, so after building up the graph we look at top level referrers.
        foreach (var component in singleFileComponentRecorder.GetDetectedComponents())
        {
            var graph = singleFileComponentRecorder.DependencyGraph;
            var explicitReferences = graph.GetExplicitReferencedDependencyIds(component.Key);
            foreach (var explicitReference in explicitReferences)
            {
                singleFileComponentRecorder.RegisterUsage(component.Value, isDevelopmentDependency: graph.IsDevelopmentDependency(explicitReference));
            }
        }
    }

    private void RecordDependencyGraphFromFileV6(PnpmYamlV6 yaml, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        // There may be multiple instance of the same package (even at the same version) in pnpm differentiated by other aspects of the pnpmDependencyPath.
        // Therefor all DetectedComponents are tracked by the same full string pnpm uses, the pnpm dependency path, which is used as the key in this dictionary.
        var components = new Dictionary<string, (DetectedComponent, Package)>();

        foreach (var (pnpmDependencyPath, package) in yaml.packages ?? Enumerable.Empty<KeyValuePair<string, Package>>())
        {
            // Ignore file: as these are local packages.
            if (pnpmDependencyPath.StartsWith("file:"))
            {
                continue;
            }

            var parentDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV6(pnpmDependencyPath: pnpmDependencyPath);
            components.Add(pnpmDependencyPath, (parentDetectedComponent, package));
        }

        foreach (var (_, (component, package)) in components)
        {
            // Each component should get registered with more detailed information later,
            // but this ensures nothing is missed due to a bug in dependency traversal or the item somehow being otherwise unreferenced.
            singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: PnpmParsingUtilities.IsPnpmPackageDevDependency(package));

            var graph = singleFileComponentRecorder.DependencyGraph;
            foreach (var (name, version) in package.dependencies ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                var pnpmDependencyPath = PnpmDependencyPath(name, version);

                // If this lookup fails, then pnpmDependencyPath is broken somehow.
                var (referenced, _) = components[pnpmDependencyPath];

                singleFileComponentRecorder.RegisterUsage(referenced, parentComponentId: component.Component.Id, isExplicitReferencedDependency: false);
            }
        }

        // "dedicated shrinkwrap" (single package) case:
        ProcessDependencySet(yaml);

        // "shared shrinkwrap" (workspace / mono-repos) case:
        foreach (var (_, package) in yaml.importers ?? Enumerable.Empty<KeyValuePair<string, PnpmHasDependenciesV6>>())
        {
            ProcessDependencySet(package);
        }

        void ProcessDependencySet(PnpmHasDependenciesV6 item)
        {
            ProcessDependencyList(item.dependencies);
            ProcessDependencyList(item.devDependencies);
            ProcessDependencyList(item.optionalDependencies);
        }

        void ProcessDependencyList(Dictionary<string, PnpmYamlV6Dependency> dependencies)
        {
            foreach (var (name, dep) in dependencies ?? Enumerable.Empty<KeyValuePair<string, PnpmYamlV6Dependency>>())
            {
                // Ignore file and link: as these are local packages.
                if (dep.version.StartsWith("link:") | dep.version.StartsWith("file:"))
                {
                    continue;
                }

                var pnpmDependencyPath = PnpmDependencyPath(name, dep.version);
                var (component, package) = components[pnpmDependencyPath];

                // Determine isDevelopmentDependency using metadata on package from pnpm rather than from which dependency list this package is under.
                // This ensures that dependencies which are a direct dev dependency and an indirect non-dev dependency get listed as non-dev.
                var isDevelopmentDependency = PnpmParsingUtilities.IsPnpmPackageDevDependency(package);

                singleFileComponentRecorder.RegisterUsage(component, isExplicitReferencedDependency: true, isDevelopmentDependency: isDevelopmentDependency);
            }
        }

        string PnpmDependencyPath(string dependencyName, string dependencyVersion)
        {
            if (dependencyVersion.StartsWith("/"))
            {
                return dependencyVersion;
            }
            else
            {
                return $"/{dependencyName}@{dependencyVersion}";
            }
        }
    }

    private bool IsLocalDependency(KeyValuePair<string, string> dependency)
    {
        // Local dependencies are dependencies that live in the file system
        // this requires an extra parsing that is not supported yet
        return dependency.Key.StartsWith("file:") || dependency.Value.StartsWith("file:") || dependency.Value.StartsWith("link:");
    }

    private string CreatePnpmPackagePathFromDependencyV5(string dependencyName, string dependencyVersion)
    {
        return dependencyVersion.Contains('/') ? dependencyVersion : $"/{dependencyName}/{dependencyVersion}";
    }
}

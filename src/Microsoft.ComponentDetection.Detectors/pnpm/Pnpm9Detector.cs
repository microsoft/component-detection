#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// There is still no official docs for the new v9 lock format, so these parsing contracts are empirically based.
/// Issue tracking v9 specs: https://github.com/pnpm/spec/issues/6
/// Format should eventually get updated here: https://github.com/pnpm/spec/blob/master/lockfile/6.0.md.
/// </summary>
public class Pnpm9Detector : IPnpmDetector
{
    public const string MajorVersion = "9";
    private readonly PnpmParsingUtilitiesBase<PnpmYamlV9> pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV9>();

    public void RecordDependencyGraphFromFile(string yamlFileContent, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var yaml = this.pnpmParsingUtilities.DeserializePnpmYamlFile(yamlFileContent);

        // There may be multiple instance of the same package (even at the same version) in pnpm differentiated by other aspects of the pnpm dependency path.
        // Therefor all DetectedComponents are tracked by the same full string pnpm uses, the pnpm dependency path, which is used as the key in this dictionary.
        // Some documentation about pnpm dependency paths can be found at https://github.com/pnpm/spec/blob/master/dependency-path.md.
        var components = new Dictionary<string, (DetectedComponent, Package)>();

        // Create a component for every package referenced in the lock file.
        // This includes all directly and transitively referenced dependencies.
        foreach (var (pnpmDependencyPath, package) in yaml.Snapshots ?? Enumerable.Empty<KeyValuePair<string, Package>>())
        {
            // Ignore "file:" as these are local packages.
            // Such local packages should only be referenced at the top level (via ProcessDependencyList) which also skips them or from other local packages (which this skips).
            // There should be no cases where a non-local package references a local package, so skipping them here should not result in failed lookups below when adding all the graph references.
            var (packageName, packageVersion) = this.pnpmParsingUtilities.ExtractNameAndVersionFromPnpmPackagePath(pnpmDependencyPath);
            var isFileOrLink = this.IsFileOrLink(packageVersion) || this.IsFileOrLink(pnpmDependencyPath);

            var dependencyPath = pnpmDependencyPath;
            if (pnpmDependencyPath.StartsWith('/'))
            {
                dependencyPath = pnpmDependencyPath[1..];
            }

            var parentDetectedComponent = this.pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath(pnpmPackagePath: dependencyPath);
            components.Add(dependencyPath, (parentDetectedComponent, package));

            // Register the component.
            // It should get registered again with with additional information (what depended on it) later,
            // but registering it now ensures nothing is missed due to a limitation in dependency traversal
            // like skipping local dependencies which might have transitively depended on this.
            if (!isFileOrLink)
            {
                singleFileComponentRecorder.RegisterUsage(parentDetectedComponent);
            }
        }

        // now that the components dictionary is populated, add direct dependencies of the current file/project setting isExplicitReferencedDependency to true
        // during this step, recursively processes any indirect dependencies
        foreach (var (_, package) in yaml.Importers ?? Enumerable.Empty<KeyValuePair<string, PnpmHasDependenciesV9>>())
        {
            this.ProcessDependencySets(singleFileComponentRecorder, components, package);
        }
    }

    private void ProcessDependencySets(ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, (DetectedComponent C, Package P)> components, PnpmHasDependenciesV9 item)
    {
        this.ProcessDependencyList(singleFileComponentRecorder, components, item.Dependencies, isDevelopmentDependency: false);
        this.ProcessDependencyList(singleFileComponentRecorder, components, item.DevDependencies, isDevelopmentDependency: true);
        this.ProcessDependencyList(singleFileComponentRecorder, components, item.OptionalDependencies, false);
    }

    private void ProcessDependencyList(ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, (DetectedComponent C, Package P)> components, Dictionary<string, PnpmYamlV9Dependency> dependencies, bool isDevelopmentDependency)
    {
        foreach (var (name, dep) in dependencies ?? Enumerable.Empty<KeyValuePair<string, PnpmYamlV9Dependency>>())
        {
            var pnpmDependencyPath = this.pnpmParsingUtilities.ReconstructPnpmDependencyPath(name, dep.Version);
            var (_, packageVersion) = this.pnpmParsingUtilities.ExtractNameAndVersionFromPnpmPackagePath(pnpmDependencyPath);
            var isFileOrLink = this.IsFileOrLink(packageVersion);

            if (isFileOrLink && !components.ContainsKey(pnpmDependencyPath))
            {
                // Link dependencies are not present in the snapshots section of the lockfile. If that's the case here, skip it.
                continue;
            }

            var (component, package) = components[pnpmDependencyPath];

            // Lockfile v9 apparently removed the tagging of dev dependencies in the lockfile, so we revert to using the dependency tree to establish dev dependency state.
            // At this point, the root dependencies are marked according to which dependency group they are declared in the lockfile itself.
            // Ignore "file:" and "link:" as these are local packages.
            if (!isFileOrLink)
            {
                singleFileComponentRecorder.RegisterUsage(component, isExplicitReferencedDependency: true, isDevelopmentDependency: isDevelopmentDependency);
            }

            var seenDependencies = new HashSet<string>();
            this.ProcessIndirectDependencies(singleFileComponentRecorder, components, isFileOrLink ? null : component.Component.Id, package.Dependencies, isDevelopmentDependency, seenDependencies);
        }
    }

    private bool IsFileOrLink(string packagePath)
    {
        return packagePath.StartsWith(PnpmConstants.PnpmLinkDependencyPath) ||
               packagePath.StartsWith(PnpmConstants.PnpmFileDependencyPath) ||
               packagePath.StartsWith(PnpmConstants.PnpmHttpDependencyPath) ||
               packagePath.StartsWith(PnpmConstants.PnpmHttpsDependencyPath);
    }

    private void ProcessIndirectDependencies(ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, (DetectedComponent C, Package P)> components, string parentComponentId, Dictionary<string, string> dependencies, bool isDevDependency, HashSet<string> seenDependencies)
    {
        // Now that the `components` dictionary is populated, make another pass of all components, registering all the dependency edges in the graph.
        foreach (var (name, version) in dependencies ?? Enumerable.Empty<KeyValuePair<string, string>>())
        {
            // Ignore "file:" and "link:" as these are local packages.
            if (this.IsFileOrLink(version))
            {
                continue;
            }

            var pnpmDependencyPath = this.pnpmParsingUtilities.ReconstructPnpmDependencyPath(name, version);
            if (seenDependencies.Contains(pnpmDependencyPath))
            {
                continue;
            }

            // If this lookup fails, then pnpmDependencyPath was either parsed incorrectly or constructed incorrectly.
            var (component, package) = components[pnpmDependencyPath];
            singleFileComponentRecorder.RegisterUsage(component, parentComponentId: parentComponentId, isExplicitReferencedDependency: false, isDevelopmentDependency: isDevDependency);
            seenDependencies.Add(pnpmDependencyPath);
            this.ProcessIndirectDependencies(singleFileComponentRecorder, components, component.Component.Id, package.Dependencies, isDevDependency, seenDependencies);
        }
    }
}

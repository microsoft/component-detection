#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

public class Pnpm6Detector : IPnpmDetector
{
    public const string MajorVersion = "6";
    private readonly PnpmParsingUtilitiesBase<PnpmYamlV6> pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV6>();

    public void RecordDependencyGraphFromFile(string yamlFileContent, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var yaml = this.pnpmParsingUtilities.DeserializePnpmYamlFile(yamlFileContent);

        // There may be multiple instance of the same package (even at the same version) in pnpm differentiated by other aspects of the pnpm dependency path.
        // Therefor all DetectedComponents are tracked by the same full string pnpm uses, the pnpm dependency path, which is used as the key in this dictionary.
        // Some documentation about pnpm dependency paths can be found at https://github.com/pnpm/spec/blob/master/dependency-path.md.
        var components = new Dictionary<string, (DetectedComponent, Package)>();

        // Create a component for every package referenced in the lock file.
        // This includes all directly and transitively referenced dependencies.
        foreach (var (pnpmDependencyPath, package) in yaml.Packages ?? Enumerable.Empty<KeyValuePair<string, Package>>())
        {
            // Ignore "file:" as these are local packages.
            // Such local packages should only be referenced at the top level (via ProcessDependencyList) which also skips them or from other local packages (which this skips).
            // There should be no cases where a non-local package references a local package, so skipping them here should not result in failed lookups below when adding all the graph references.
            if (pnpmDependencyPath.StartsWith(PnpmConstants.PnpmFileDependencyPath))
            {
                continue;
            }

            var parentDetectedComponent = this.pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath(pnpmPackagePath: pnpmDependencyPath);
            components.Add(pnpmDependencyPath, (parentDetectedComponent, package));

            // Register the component.
            // It should get registered again with with additional information (what depended on it) later,
            // but registering it now ensures nothing is missed due to a limitation in dependency traversal
            // like skipping local dependencies which might have transitively depended on this.
            singleFileComponentRecorder.RegisterUsage(parentDetectedComponent, isDevelopmentDependency: this.pnpmParsingUtilities.IsPnpmPackageDevDependency(package));
        }

        // Now that the `components` dictionary is populated, make a second pass registering all the dependency edges in the graph.
        foreach (var (_, (component, package)) in components)
        {
            foreach (var (name, version) in package.Dependencies ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                var pnpmDependencyPath = this.pnpmParsingUtilities.ReconstructPnpmDependencyPath(name, version);

                // If this lookup fails, then pnpmDependencyPath was either parsed incorrectly or constructed incorrectly.
                var (referenced, _) = components[pnpmDependencyPath];

                singleFileComponentRecorder.RegisterUsage(referenced, parentComponentId: component.Component.Id, isExplicitReferencedDependency: false);
            }
        }

        // Lastly, add all direct dependencies of the current file/project setting isExplicitReferencedDependency to true:

        // "dedicated shrinkwrap" (single package) case:
        this.ProcessDependencySet(singleFileComponentRecorder, components, yaml);

        // "shared shrinkwrap" (workspace / mono-repos) case:
        foreach (var (_, package) in yaml.Importers ?? Enumerable.Empty<KeyValuePair<string, PnpmHasDependenciesV6>>())
        {
            this.ProcessDependencySet(singleFileComponentRecorder, components, package);
        }
    }

    private void ProcessDependencySet(ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, (DetectedComponent C, Package P)> components, PnpmHasDependenciesV6 item)
    {
        this.ProcessDependencyList(singleFileComponentRecorder, components, item.Dependencies);
        this.ProcessDependencyList(singleFileComponentRecorder, components, item.DevDependencies);
        this.ProcessDependencyList(singleFileComponentRecorder, components, item.OptionalDependencies);
    }

    private void ProcessDependencyList(ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, (DetectedComponent C, Package P)> components, Dictionary<string, PnpmYamlV6Dependency> dependencies)
    {
        foreach (var (name, dep) in dependencies ?? Enumerable.Empty<KeyValuePair<string, PnpmYamlV6Dependency>>())
        {
            // Ignore "file:" and "link:" as these are local packages.
            if (dep.Version.StartsWith(PnpmConstants.PnpmLinkDependencyPath) || dep.Version.StartsWith(PnpmConstants.PnpmFileDependencyPath))
            {
                continue;
            }

            var pnpmDependencyPath = this.pnpmParsingUtilities.ReconstructPnpmDependencyPath(name, dep.Version);
            var (component, package) = components[pnpmDependencyPath];

            // Determine isDevelopmentDependency using metadata on package from pnpm rather than from which dependency list this package is under.
            // This ensures that dependencies which are a direct dev dependency and an indirect non-dev dependency get listed as non-dev.
            var isDevelopmentDependency = this.pnpmParsingUtilities.IsPnpmPackageDevDependency(package);

            singleFileComponentRecorder.RegisterUsage(component, isExplicitReferencedDependency: true, isDevelopmentDependency: isDevelopmentDependency);
        }
    }
}

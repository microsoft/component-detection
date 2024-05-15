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

public class Pnpm6ComponentDetector : FileComponentDetector, IExperimentalDetector
{
    public Pnpm6ComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<Pnpm6ComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "Pnpm6";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "shrinkwrap.yaml", "pnpm-lock.yaml" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

    public override int Version { get; } = 1;

    public override bool NeedsAutomaticRootDependencyCalculation => true;

    /// <inheritdoc />
    protected override IList<string> SkippedFolders => new List<string> { "node_modules", "pnpm-store" };

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

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
            var majorVersion = version?.Split(".")[0];
            switch (majorVersion)
            {
                case null:
                case "5":
                    // Handled in the non-experimental detector. No-op here.
                    break;
                case "6":
                    var pnpmYamlV6 = PnpmParsingUtilities.DeserializePnpmYamlV6File(fileContent);
                    this.RecordDependencyGraphFromFileV6(pnpmYamlV6, singleFileComponentRecorder);
                    break;
                default:
                    // Handled in the non-experimental detector. No-op here.
                    break;
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read pnpm yaml file {File}", file.Location);
        }
    }

    private void RecordDependencyGraphFromFileV6(PnpmYamlV6 yaml, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
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
            if (pnpmDependencyPath.StartsWith("file:"))
            {
                continue;
            }

            var parentDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV6(pnpmDependencyPath: pnpmDependencyPath);
            components.Add(pnpmDependencyPath, (parentDetectedComponent, package));

            // Register the component.
            // It should get registered again with with additional information (what depended on it) later,
            // but registering it now ensures nothing is missed due to a limitation in dependency traversal
            // like skipping local dependencies which might have transitively depended on this.
            singleFileComponentRecorder.RegisterUsage(parentDetectedComponent, isDevelopmentDependency: PnpmParsingUtilities.IsPnpmPackageDevDependency(package));
        }

        // Now that the `components` dictionary is populated, make a second pass registering all the dependency edges in the graph.
        foreach (var (_, (component, package)) in components)
        {
            foreach (var (name, version) in package.Dependencies ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                var pnpmDependencyPath = PnpmParsingUtilities.ReconstructPnpmDependencyPathV6(name, version);

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
            if (dep.Version.StartsWith("link:") || dep.Version.StartsWith("file:"))
            {
                continue;
            }

            var pnpmDependencyPath = PnpmParsingUtilities.ReconstructPnpmDependencyPathV6(name, dep.Version);
            var (component, package) = components[pnpmDependencyPath];

            // Determine isDevelopmentDependency using metadata on package from pnpm rather than from which dependency list this package is under.
            // This ensures that dependencies which are a direct dev dependency and an indirect non-dev dependency get listed as non-dev.
            var isDevelopmentDependency = PnpmParsingUtilities.IsPnpmPackageDevDependency(package);

            singleFileComponentRecorder.RegisterUsage(component, isExplicitReferencedDependency: true, isDevelopmentDependency: isDevelopmentDependency);
        }
    }
}

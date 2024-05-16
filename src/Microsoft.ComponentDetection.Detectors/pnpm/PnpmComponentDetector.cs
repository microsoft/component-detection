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
        this.Logger = logger;
    }

    public override string Id { get; } = "Pnpm";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "shrinkwrap.yaml", "pnpm-lock.yaml" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

    public override int Version { get; } = 5;

    public override bool NeedsAutomaticRootDependencyCalculation => true;

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
            var majorVersion = version?.Split(".")[0];
            switch (majorVersion)
            {
                case null:
                // The null case falls through to version 5 to preserver the behavior of this scanner from before version specific logic was added.
                // This allows files versioned with "shrinkwrapVersion" (such as one included in some of the tests) to be used.
                // Given that "shrinkwrapVersion" is a concept from file format version 4 https://github.com/pnpm/spec/blob/master/lockfile/4.md)
                // this case might not be robust.
                case "5":
                    var pnpmYamlV5 = PnpmParsingUtilities.DeserializePnpmYamlV5File(fileContent);
                    this.RecordDependencyGraphFromFileV5(pnpmYamlV5, singleFileComponentRecorder);
                    break;
                case "6":
                    // Handled in the experimental detector. No-op here.
                    break;
                default:
                    this.Logger.LogWarning("Unsupported lockfileVersion in pnpm yaml file {File}", file.Location);
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
        foreach (var packageKeyValue in yaml.Packages ?? Enumerable.Empty<KeyValuePair<string, Package>>())
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

            if (packageKeyValue.Value.Dependencies != null)
            {
                foreach (var dependency in packageKeyValue.Value.Dependencies)
                {
                    // Ignore local packages.
                    if (PnpmParsingUtilities.IsLocalDependency(dependency))
                    {
                        continue;
                    }

                    var childDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV5(
                        pnpmPackagePath: PnpmParsingUtilities.CreatePnpmPackagePathFromDependencyV5(dependency.Key, dependency.Value));

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
}

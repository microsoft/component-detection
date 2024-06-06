namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

public class Pnpm5Detector : IPnpmDetector
{
    public const string MajorVersion = "5";

    public void RecordDependencyGraphFromFile(string yamlFileContent, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var yaml = PnpmParsingUtilities.DeserializePnpmYamlV5File(yamlFileContent);

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

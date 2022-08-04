using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Detectors.Pnpm
{
    [Export(typeof(IComponentDetector))]
    public class PnpmComponentDetector : FileComponentDetector
    {
        public override string Id { get; } = "Pnpm";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "shrinkwrap.yaml", "pnpm-lock.yaml" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

        public override int Version { get; } = 5;

        /// <inheritdoc />
        protected override IList<string> SkippedFolders => new List<string> { "node_modules", "pnpm-store" };

        public PnpmComponentDetector()
        {
            this.NeedsAutomaticRootDependencyCalculation = true;
        }

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            this.Logger.LogVerbose("Found yaml file: " + file.Location);
            string skippedFolder = this.SkippedFolders.FirstOrDefault(folder => file.Location.Contains(folder));
            if (!string.IsNullOrEmpty(skippedFolder))
            {
                this.Logger.LogVerbose($"Skipping found file, it was detected as being within a {skippedFolder} folder.");
            }

            try
            {
                var pnpmYaml = await PnpmParsingUtilities.DeserializePnpmYamlFile(file);
                this.RecordDependencyGraphFromFile(pnpmYaml, singleFileComponentRecorder);
            }
            catch (Exception e)
            {
                this.Logger.LogFailedReadingFile(file.Location, e);
            }
        }

        private void RecordDependencyGraphFromFile(PnpmYaml yaml, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            foreach (var packageKeyValue in yaml.packages ?? Enumerable.Empty<KeyValuePair<string, Package>>())
            {
                // Ignore file: as these are local packages.
                if (packageKeyValue.Key.StartsWith("file:"))
                {
                    continue;
                }

                var parentDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath(pnpmPackagePath: packageKeyValue.Key);
                bool isDevDependency = packageKeyValue.Value != null && PnpmParsingUtilities.IsPnpmPackageDevDependency(packageKeyValue.Value);
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

                        var childDetectedComponent = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath(pnpmPackagePath: this.CreatePnpmPackagePathFromDependency(dependency.Key, dependency.Value));

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

        private bool IsLocalDependency(KeyValuePair<string, string> dependency)
        {
            // Local dependencies are dependencies that live in the file system
            // this requires an extra parsing that is not supported yet
            return dependency.Key.StartsWith("file:") || dependency.Value.StartsWith("file:") || dependency.Value.StartsWith("link:");
        }

        private string CreatePnpmPackagePathFromDependency(string dependencyName, string dependencyVersion)
        {
            return dependencyVersion.Contains('/') ? dependencyVersion : $"/{dependencyName}/{dependencyVersion}";
        }
    }
}

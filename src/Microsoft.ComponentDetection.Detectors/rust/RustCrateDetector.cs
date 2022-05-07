using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Nett;

namespace Microsoft.ComponentDetection.Detectors.Rust
{
    [Export(typeof(IComponentDetector))]
    public class RustCrateDetector : FileComponentDetector
    {
        public override string Id => "RustCrateDetector";

        public override IList<string> SearchPatterns => new List<string> { CargoLockSearchPattern };

        public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Cargo };

        public override int Version { get; } = 8;

        public override IEnumerable<string> Categories => new List<string> { "Rust" };

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var cargoLockFile = processRequest.ComponentStream;

            try
            {
                var cargoLock = StreamTomlSerializer.Deserialize(cargoLockFile.Stream, TomlSettings.Create()).Get<CargoLock>();

                var seenAsDependency = new HashSet<CargoPackage>();

                // Pass 1: Create typed components and allow lookup by name.
                var packagesByName = new Dictionary<string, List<(CargoPackage package, CargoComponent component)>>();
                if (cargoLock.package != null)
                {
                    foreach (var cargoPackage in cargoLock.package)
                    {
                        // Get or create the list of packages with this name
                        if (packagesByName.TryGetValue(cargoPackage.name, out var packageList))
                        {
                            if (packageList.Any(p => p.package.Equals(cargoPackage)))
                            {
                                // Ignore duplicate packages
                                continue;
                            }
                        }
                        else
                        {
                            packageList = new List<(CargoPackage, CargoComponent)>();
                            packagesByName.Add(cargoPackage.name, packageList);
                        }

                        // Create a node for each non-local package to allow adding dependencies later.
                        CargoComponent cargoComponent = null;
                        if (!IsLocalPackage(cargoPackage))
                        {
                            cargoComponent = new CargoComponent(cargoPackage.name, cargoPackage.version, cargoPackage.checksum, cargoPackage.source);
                            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(cargoComponent));
                        }
                        
                        // Add the package/component pair to the list
                        packageList.Add((cargoPackage, cargoComponent));
                    }

                    // Pass 2: Register dependencies.
                    foreach (var packageList in packagesByName.Values)
                    {
                        // Get the parent package and component
                        foreach (var (parentPackage, parentComponent) in packageList)
                        {
                            if (parentPackage.dependencies == null)
                            {
                                // This package has no dependency edges to contribute.
                                continue;
                            }

                            // Process each dependency
                            foreach (var dependency in parentPackage.dependencies)
                            {
                                ProcessDependency(cargoLockFile, singleFileComponentRecorder, seenAsDependency, packagesByName, parentPackage, parentComponent, dependency);
                            }
                        }
                    }

                    // Pass 3: Conservatively mark packages we found no dependency to as roots
                    foreach (var packageList in packagesByName.Values)
                    {
                        // Get the package and component.
                        foreach (var (package, component) in packageList)
                        {
                            if (!IsLocalPackage(package) && !seenAsDependency.Contains(package))
                            {
                                var detectedComponent = new DetectedComponent(component);
                                singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: true);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // If something went wrong, just ignore the file
                Logger.LogFailedReadingFile(cargoLockFile.Location, e);
            }

            return Task.CompletedTask;
        }

        private void ProcessDependency(
            IComponentStream cargoLockFile,
            ISingleFileComponentRecorder singleFileComponentRecorder,
            HashSet<CargoPackage> seenAsDependency,
            Dictionary<string, List<(CargoPackage package, CargoComponent component)>> packagesByName,
            CargoPackage parentPackage,
            CargoComponent parentComponent,
            string dependency)
        {
            try
            {
                // Extract the informationfrom the dependency (name with optional version and source)
                if (!ParseDependency(dependency, out var childName, out var childVersion, out var childSource))
                {
                    // Could not parse the dependency string
                    throw new FormatException($"Failed to parse dependency '{dependency}'");
                }

                if (!packagesByName.TryGetValue(childName, out var candidatePackages))
                {
                    throw new FormatException($"Could not find any package named '{childName}' for depenency string '{dependency}'");
                }

                // Search through the list of candidates to find a match (note that version and source are optional).
                CargoPackage childPackage = null;
                CargoComponent childComponent = null;
                foreach (var (candidatePackage, candidateComponent) in candidatePackages)
                {
                    if (childVersion != null && candidatePackage.version != childVersion)
                    {
                        // This does not have the requested version
                        continue;
                    }

                    if (childSource != null && candidatePackage.source != childSource)
                    {
                        // This does not have the requested source
                        continue;
                    }

                    if (childPackage != null)
                    {
                        throw new FormatException($"Found multiple matching packages for dependency string '{dependency}'");
                    }

                    // We have found the requested package.
                    childPackage = candidatePackage;
                    childComponent = candidateComponent;
                }

                if (childPackage == null)
                {
                    throw new FormatException($"Could not find matching package for dependency string '{dependency}'");
                }

                if (IsLocalPackage(childPackage))
                {
                    if (!IsLocalPackage(parentPackage))
                    {
                        throw new FormatException($"In package with source '{parentComponent.Id}' found non-source dependency string: '{dependency}'");
                    }

                    // This is a dependency between packages without source
                    return;
                }

                var detectedComponent = new DetectedComponent(childComponent);
                seenAsDependency.Add(childPackage);

                if (IsLocalPackage(parentPackage))
                {
                    // We are adding a root edge (from a local package)
                    singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: true);
                }
                else
                {
                    // we are adding an edge within the graph
                    singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: false, parentComponentId: parentComponent.Id);
                }
            }
            catch (Exception e)
            {
                using var record = new RustCrateDetectorTelemetryRecord();

                record.PackageInfo = $"{parentPackage.name}, {parentPackage.version}, {parentPackage.source}";
                record.Dependencies = dependency;

                Logger.LogFailedReadingFile(cargoLockFile.Location, e);
            }
        }

        private static readonly Regex DependencyFormatRegex = new Regex(
           ////  PkgName[ Version][ (Source)]
           @"([^ ]+)(?: ([^ ]+))?(?: \(([^()]*)\))?",
           RegexOptions.Compiled);

        private const int PackageNameGroup = 1;
        private const int VersionGroup = 2;
        private const int SourceGroup = 3;

        private static bool ParseDependency(string dependency, out string packageName, out string version, out string source)
        {
            var match = DependencyFormatRegex.Match(dependency);
            packageName = match.Groups[PackageNameGroup].Success ? match.Groups[PackageNameGroup].Value : null;
            version = match.Groups[VersionGroup].Success ? match.Groups[VersionGroup].Value : null;
            source = match.Groups[SourceGroup].Success ? match.Groups[SourceGroup].Value : null;

            if (source == string.Empty)
            {
                source = null;
            }

            return match.Success;
        }

        private static bool IsLocalPackage(CargoPackage package) => package.source == null;

        private const string CargoLockSearchPattern = "Cargo.lock";

        private static string MakeDependencyStringFromPackage(CargoPackage package)
        {
            return $"{package.name} {package.version} ({package.source})";
        }
    }
}

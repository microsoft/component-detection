using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotNet.Globbing;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Tomlyn;
using Semver;
using System.Threading.Tasks;
using Tomlyn.Model;

namespace Microsoft.ComponentDetection.Detectors.Rust
{
    public class RustCrateUtilities
    {
        private static readonly Regex DependencyFormatRegex = new Regex(
        ////  PkgName Version    Source
            @"([^ ]+) ([^ ]+) \(([^()]*)\)",
            RegexOptions.Compiled);

        public const string CargoTomlSearchPattern = "Cargo.toml";
        public const string CargoLockSearchPattern = "Cargo.lock";

        public static string[] NonDevDependencyKeys => new string[] { "dependencies", "build-dependencies" };

        public static string[] DevDependencyKeys => new string[] { "dev-dependencies" };

        private const string WorkspaceKey = "workspace";

        private const string WorkspaceMemberKey = "members";

        private const string WorkspaceExcludeKey = "exclude";

        /// <summary>
        /// Given the project root Cargo.toml file, extract any workspaces specified and any root dependencies.
        /// </summary>
        /// <param name="cargoTomlComponentStream">A stream representing the root cargo.toml file.</param>
        /// <param name="singleFileComponentRecorder">The component recorder which will have workspace toml files added as related.</param>
        /// <returns>
        /// A CargoDependencyData containing populated lists of CargoWorkspaces that will be included from search, CargoWorkspaceExclusions that will be excluded from search,
        /// a list of non-development dependencies, and a list of development dependencies.
        /// </returns>
        public static async Task<CargoDependencyData> ExtractRootDependencyAndWorkspaceSpecificationsAsync(IEnumerable<IComponentStream> cargoTomlComponentStream, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            var cargoDependencyData = new CargoDependencyData();

            // The file handle is disposed if you call .First() on cargoTomlComponentStream
            // Since multiple Cargo.toml files for 1 Cargo.lock file obviously doesn't make sense
            // We break at the end of this loop
            foreach (var cargoTomlFile in cargoTomlComponentStream)
            {
                var reader = new StreamReader(cargoTomlFile.Stream);
                var cargoToml = Toml.ToModel(await reader.ReadToEndAsync());
                //var cargoToml = StreamTomlSerializer.Deserialize(cargoTomlFile.Stream, TomlSettings.Create());

                singleFileComponentRecorder.AddAdditionalRelatedFile(cargoTomlFile.Location);

                // Extract the workspaces present, if any
                if (cargoToml.ContainsKey(WorkspaceKey))
                {
                    var workspaces = cargoToml["WorkspaceKey"] as TomlTable;

                    var workspaceMembers = workspaces.ContainsKey(WorkspaceMemberKey) ? workspaces[WorkspaceMemberKey] as TomlObject : null;
                    var workspaceExclusions = workspaces.ContainsKey(WorkspaceExcludeKey) ? workspaces[WorkspaceExcludeKey] as TomlObject : null;

                    if (workspaceMembers != null)
                    {
                        if (workspaceMembers.TomlType != TomlObjectType.Array)
                        {
                            throw new InvalidRustTomlFileException($"In accompanying Cargo.toml file expected {WorkspaceMemberKey} within {WorkspaceKey} to be of type Array, but found {workspaceMembers.GetType}");
                        }

                        // TomlObject arrays do not natively implement a HashSet get, so add from a list
                        cargoDependencyData.CargoWorkspaces.UnionWith(workspaceMembers.Get<List<string>>());
                    }

                    if (workspaceExclusions != null)
                    {
                        if (workspaceExclusions.TomlType != TomlObjectType.Array)
                        {
                            throw new InvalidRustTomlFileException($"In accompanying Cargo.toml file expected {WorkspaceExcludeKey} within {WorkspaceKey} to be of type Array, but found {workspaceExclusions.GetType}");
                        }

                        cargoDependencyData.CargoWorkspaceExclusions.UnionWith(workspaceExclusions.Get<List<string>>());
                    }
                }

                GenerateDependencies(cargoToml, cargoDependencyData.NonDevDependencies, cargoDependencyData.DevDependencies);

                break;
            }

            return cargoDependencyData;
        }

        /// <summary>
        /// Given a set of Cargo.toml files, extract development and non-development dependency lists for each.
        /// </summary>
        /// <param name="cargoTomlComponentStreams">A list of streams representing cargo workspaces.</param>
        /// <param name="singleFileComponentRecorder">The component recorder which will have workspace toml files added as related.</param>
        /// <param name="nonDevDependencySpecifications">Current list of non-development dependencies.</param>
        /// <param name="devDependencySpecifications">Current list of development dependencies.</param>
        public static void ExtractDependencySpecifications(IEnumerable<IComponentStream> cargoTomlComponentStreams, ISingleFileComponentRecorder singleFileComponentRecorder, IList<DependencySpecification> nonDevDependencySpecifications, IList<DependencySpecification> devDependencySpecifications)
        {
            // The file handles within cargoTomlComponentStreams will be disposed after enumeration
            // This method is only used in non root toml extraction, so the whole list should be iterated
            foreach (var cargoTomlFile in cargoTomlComponentStreams)
            {
                var cargoToml = StreamTomlSerializer.Deserialize(cargoTomlFile.Stream, TomlSettings.Create());

                singleFileComponentRecorder.AddAdditionalRelatedFile(cargoTomlFile.Location);

                GenerateDependencies(cargoToml, nonDevDependencySpecifications, devDependencySpecifications);
            }
        }

        /// <summary>
        /// Extract development and non-development dependency lists from a given TomlTable.
        /// </summary>
        /// <param name="cargoToml">The TomlTable representing a whole cargo.toml file.</param>
        /// <param name="nonDevDependencySpecifications">Current list of non-development dependencies.</param>
        /// <param name="devDependencySpecifications">Current list of development dependencies.</param>
        private static void GenerateDependencies(TomlTable cargoToml, IList<DependencySpecification> nonDevDependencySpecifications, IList<DependencySpecification> devDependencySpecifications)
        {
            var dependencySpecification = GenerateDependencySpecifications(cargoToml, NonDevDependencyKeys);
            var devDependencySpecification = GenerateDependencySpecifications(cargoToml, DevDependencyKeys);

            // If null, this indicates the toml is an internal file that should not be tracked as a component.
            if (dependencySpecification != null)
            {
                nonDevDependencySpecifications.Add(dependencySpecification);
            }

            if (devDependencySpecification != null)
            {
                devDependencySpecifications.Add(devDependencySpecification);
            }
        }

        /// <summary>
        /// Generate a predicate which will be used to exclude directories which should not contain cargo.toml files.
        /// </summary>
        /// <param name="rootLockFileInfo">The FileInfo for the cargo.lock file found in the root directory.</param>
        /// <param name="definedWorkspaces">A list of relative folder paths to include in search.</param>
        /// <param name="definedExclusions">A list of relative folder paths to exclude from search.</param>
        /// <returns></returns>
        public static ExcludeDirectoryPredicate BuildExcludeDirectoryPredicateFromWorkspaces(FileInfo rootLockFileInfo, HashSet<string> definedWorkspaces, HashSet<string> definedExclusions)
        {
            var workspaceGlobs = BuildGlobMatchingFromWorkspaces(rootLockFileInfo, definedWorkspaces);

            // Since the paths come in as relative, make them fully qualified
            var fullyQualifiedExclusions = definedExclusions.Select(x => Path.Combine(rootLockFileInfo.DirectoryName, x)).ToHashSet();

            // The predicate will be evaluated with the current directory name to search and the full path of its parent. Return true when it should be excluded from search.
            return (ReadOnlySpan<char> nameOfDirectoryToConsider, ReadOnlySpan<char> pathOfParentOfDirectoryToConsider) =>
            {
                var currentPath = Path.Combine(pathOfParentOfDirectoryToConsider.ToString(), nameOfDirectoryToConsider.ToString());

                return !workspaceGlobs.Values.Any(x => x.IsMatch(currentPath)) || fullyQualifiedExclusions.Contains(currentPath);
            };
        }

        /// <summary>
        /// Generates a list of Glob compatible Cargo workspace directories which will be searched. See https://docs.rs/glob/0.3.0/glob/struct.Pattern.html for glob patterns.
        /// </summary>
        /// <param name="rootLockFileInfo">The FileInfo for the cargo.lock file found in the root directory.</param>
        /// <param name="definedWorkspaces">A list of relative folder paths to include in search.</param>
        /// <returns></returns>
        private static Dictionary<string, Glob> BuildGlobMatchingFromWorkspaces(FileInfo rootLockFileInfo, HashSet<string> definedWorkspaces)
        {
            var directoryGlobs = new Dictionary<string, Glob>
            {
                { rootLockFileInfo.DirectoryName, Glob.Parse(rootLockFileInfo.DirectoryName) },
            };

            // For the given workspaces, add their paths to search list
            foreach (var workspace in definedWorkspaces)
            {
                var currentPath = rootLockFileInfo.DirectoryName;
                var directoryPathParts = workspace.Split('/');

                // When multiple levels of subdirectory are present, each directory parent must be added or the directory will not be reached
                // For example, ROOT/test-space/first-test/src/Cargo.toml requires the following directories be matched:
                // ROOT/test-space, ROOT/test-space/first-test, ROOT/test-space/first-test, ROOT/test-space/first-test/src
                // Each directory is matched explicitly instead of performing a StartsWith due to the potential of Glob character matching
                foreach (var pathPart in directoryPathParts)
                {
                    currentPath = Path.Combine(currentPath, pathPart);
                    directoryGlobs[currentPath] = Glob.Parse(currentPath);
                }
            }

            return directoryGlobs;
        }

        public static void BuildGraph(HashSet<CargoPackage> cargoPackages, IList<DependencySpecification> nonDevDependencies, IList<DependencySpecification> devDependencies, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            // Get all root components that are not dev dependencies
            // This is a bug:
            // Say Cargo.toml defined async ^1.0 as a dependency
            // Say Cargo.lock has async 1.0.0 and async 1.0.2
            // Both will be marked as root and there's no way to tell which one is "real"
            IList<CargoPackage> nonDevRoots = cargoPackages
                                                .Where(detectedComponent => IsCargoPackageInDependencySpecifications(detectedComponent, nonDevDependencies))
                                                .ToList();

            // Get all roots that are dev deps
            IList<CargoPackage> devRoots = cargoPackages
                                                .Where(detectedComponent => IsCargoPackageInDependencySpecifications(detectedComponent, devDependencies))
                                                .ToList();

            var packagesDict = cargoPackages.ToDictionary(cargoPackage => new CargoComponent(cargoPackage.name, cargoPackage.version).Id);

            FollowRoots(packagesDict, devRoots, singleFileComponentRecorder, true);
            FollowRoots(packagesDict, nonDevRoots, singleFileComponentRecorder, false);
        }

        private static void FollowRoots(Dictionary<string, CargoPackage> packagesDict, IList<CargoPackage> roots, ISingleFileComponentRecorder singleFileComponentRecorder, bool isDevDependencies)
        {
            var componentQueue = new Queue<(string, CargoPackage)>();
            roots.ToList().ForEach(devRootDetectedComponent => componentQueue.Enqueue((null, devRootDetectedComponent)));

            var visited = new HashSet<string>();

            // All of these components will be dev deps
            while (componentQueue.Count > 0)
            {
                var (parentId, currentPackage) = componentQueue.Dequeue();
                var currentComponent = CargoPackageToCargoComponent(currentPackage);

                if (visited.Contains(currentComponent.Id))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(parentId)) // This is a root component
                {
                    AddOrUpdateDetectedComponent(singleFileComponentRecorder, currentComponent, isDevDependencies, isExplicitReferencedDependency: true);
                }
                else
                {
                    AddOrUpdateDetectedComponent(singleFileComponentRecorder, currentComponent, isDevDependencies, parentComponentId: parentId);
                }

                visited.Add(currentComponent.Id);

                if (currentPackage.dependencies != null && currentPackage.dependencies.Any())
                {
                    foreach (var dependency in currentPackage.dependencies)
                    {
                        var regexMatch = DependencyFormatRegex.Match(dependency);
                        if (regexMatch.Success)
                        {
                            if (SemVersion.TryParse(regexMatch.Groups[2].Value, out var sv))
                            {
                                var name = regexMatch.Groups[1].Value;
                                var version = sv.ToString();
                                var source = regexMatch.Groups[3].Value;

                                packagesDict.TryGetValue(new CargoComponent(name, version).Id, out var dependencyPackage);

                                componentQueue.Enqueue((currentComponent.Id, dependencyPackage));
                            }
                            else
                            {
                                throw new FormatException($"Could not parse {regexMatch.Groups[2].Value} into a valid Semver");
                            }
                        }
                        else
                        {
                            throw new FormatException("Could not parse: " + dependency);
                        }
                    }
                }
            }
        }

        private static DetectedComponent AddOrUpdateDetectedComponent(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            TypedComponent component,
            bool isDevDependency,
            string parentComponentId = null,
            bool isExplicitReferencedDependency = false)
        {
            var newComponent = new DetectedComponent(component);
            singleFileComponentRecorder.RegisterUsage(newComponent, isExplicitReferencedDependency, parentComponentId: parentComponentId, isDevelopmentDependency: isDevDependency);
            var recordedComponent = singleFileComponentRecorder.GetComponent(newComponent.Component.Id);
            recordedComponent.DevelopmentDependency &= isDevDependency;

            return recordedComponent;
        }

        public static DependencySpecification GenerateDependencySpecifications(TomlTable cargoToml, IEnumerable<string> tomlDependencyKeys)
        {
            var dependencySpecifications = new DependencySpecification();
            var dependencyLocations = GetDependencies(cargoToml, tomlDependencyKeys);
            foreach (var dependencies in dependencyLocations)
            {
                foreach (var dependency in dependencies.Keys)
                {
                    string versionSpecifier;
                    if (dependencies[dependency].TomlType == TomlObjectType.String)
                    {
                        versionSpecifier = dependencies.Get<string>(dependency);
                    }
                    else if (dependencies.Get<TomlTable>(dependency).ContainsKey("version") && dependencies.Get<TomlTable>(dependency).Get<string>("version") != "0.0.0")
                    {
                        // We have a valid version that doesn't indicate 'internal' like 0.0.0 does.
                        versionSpecifier = dependencies.Get<TomlTable>(dependency).Get<string>("version");
                    }
                    else if (dependencies.Get<TomlTable>(dependency).ContainsKey("path"))
                    {
                        // If this is a workspace dependency specification that specifies a component by path reference, skip adding it directly here.
                        // Example: kubos-app = { path = "../../apis/app-api/rust" }
                        continue;
                    }
                    else
                    {
                        return null;
                    }

                    // If the dependency is renamed, use the actual name of the package:
                    // https://doc.rust-lang.org/cargo/reference/specifying-dependencies.html#renaming-dependencies-in-cargotoml
                    string dependencyName;
                    if (dependencies[dependency].TomlType == TomlObjectType.Table &&
                        dependencies.Get<TomlTable>(dependency).ContainsKey("package"))
                    {
                        dependencyName = dependencies.Get<TomlTable>(dependency).Get<string>("package");
                    }
                    else
                    {
                        dependencyName = dependency;
                    }

                    dependencySpecifications.Add(dependencyName, versionSpecifier);
                }
            }

            return dependencySpecifications;
        }

        public static IEnumerable<CargoPackage> ConvertCargoLockV2PackagesToV1(CargoLock cargoLock)
        {
            var packageMap = new Dictionary<string, List<CargoPackage>>();
            cargoLock.package.ToList().ForEach(package =>
            {
                if (!packageMap.TryGetValue(package.name, out var packageList))
                {
                    packageMap[package.name] = new List<CargoPackage>() { package };
                }
                else
                {
                    packageList.Add(package);
                }
            });

            return cargoLock.package.Select(package =>
            {
                if (package.dependencies == null)
                {
                    return package;
                }

                try
                {
                    // We're just formatting the v2 dependencies in the v1 way
                    package.dependencies = package.dependencies
                    .Select(dep =>
                    {
                        // parts[0] => name
                        // parts[1] => version
                        var parts = dep.Split(' ');

                        // Using v1 format, nothing to change
                        if (string.IsNullOrEmpty(dep) || parts.Length == 3)
                        {
                            return dep;
                        }

                        // Below 2 cases use v2 format
                        else if (parts.Length == 1)
                        {
                            // There should only be 1 package in packageMap with this name since we don't specify a version
                            // We want this to throw if we find more than 1 package because it means there is ambiguity about which package is being used
                            var mappedPackage = packageMap[parts[0]].Single();

                            return MakeDependencyStringFromPackage(mappedPackage);
                        }
                        else if (parts.Length == 2)
                        {
                            // Search for the package name + version
                            // Throws if more than 1 for same reason as above
                            var mappedPackage = packageMap[parts[0]].Where(subPkg => subPkg.version == parts[1]).Single();

                            return MakeDependencyStringFromPackage(mappedPackage);
                        }

                        throw new FormatException($"Did not expect the dependency string {dep} to have more than 3 parts");
                    }).ToArray();
                }
                catch
                {
                    using var record = new RustCrateV2DetectorTelemetryRecord();

                    record.PackageInfo = $"{package.name}, {package.version}, {package.source}";
                    record.Dependencies = string.Join(',', package.dependencies);
                }

                return package;
            });
        }

        public static string MakeDependencyStringFromPackage(CargoPackage package)
        {
            return $"{package.name} {package.version} ({package.source})";
        }

        private static CargoPackage DependencyStringToCargoPackage(string depString)
        {
            var regexMatch = DependencyFormatRegex.Match(depString);
            if (regexMatch.Success)
            {
                if (SemVersion.TryParse(regexMatch.Groups[2].Value, out var sv))
                {
                    var dependencyPackage = new CargoPackage
                    {
                        name = regexMatch.Groups[1].Value,
                        version = sv.ToString(),
                        source = regexMatch.Groups[3].Value,
                    };
                    return dependencyPackage;
                }

                throw new FormatException($"Could not parse {regexMatch.Groups[2].Value} into a valid Semver");
            }

            throw new FormatException("Could not parse: " + depString);
        }

        private static bool IsCargoPackageInDependencySpecifications(CargoPackage cargoPackage, IList<DependencySpecification> dependencySpecifications)
        {
            return dependencySpecifications
                        .Where(dependencySpecification => dependencySpecification.MatchesPackage(cargoPackage))
                        .Any();
        }

        private static TypedComponent CargoPackageToCargoComponent(CargoPackage cargoPackage)
        {
            return new CargoComponent(cargoPackage.name, cargoPackage.version);
        }

        private static IEnumerable<TomlTable> GetDependencies(TomlTable cargoToml, IEnumerable<string> tomlDependencyKeys)
        {
            const string targetKey = "target";
            var dependencies = new List<TomlTable>();

            foreach (var tomlDependencyKey in tomlDependencyKeys)
            {
                if (cargoToml.ContainsKey(tomlDependencyKey))
                {
                    dependencies.Add(cargoToml.Get<TomlTable>(tomlDependencyKey));
                }
            }

            if (cargoToml.ContainsKey(targetKey))
            {
                var configs = cargoToml.Get<TomlTable>(targetKey);
                foreach (var config in configs)
                {
                    var properties = configs.Get<TomlTable>(config.Key);
                    foreach (var propertyKey in properties.Keys)
                    {
                        var isRelevantKey = tomlDependencyKeys.Any(dependencyKey =>
                            string.Equals(propertyKey, dependencyKey, StringComparison.InvariantCultureIgnoreCase));

                        if (isRelevantKey)
                        {
                            dependencies.Add(properties.Get<TomlTable>(propertyKey));
                        }
                    }
                }
            }

            return dependencies;
        }
    }
}

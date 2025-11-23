#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.Extensions.Logging;
using Tomlyn;

/// <summary>
/// Detector for Cargo.lock files.
/// </summary>
public class RustCargoLockParser : IRustCargoLockParser
{
    ////  PkgName[ Version][ (Source)]
    private static readonly Regex DependencyFormatRegex = new Regex(
        @"^(?<packageName>[^ ]+)(?: (?<version>[^ ]+))?(?: \((?<source>[^()]*)\))?$",
        RegexOptions.Compiled);

    private static readonly TomlModelOptions TomlOptions = new TomlModelOptions
    {
        IgnoreMissingProperties = true,
    };

    private readonly ILogger<RustCargoLockParser> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustCargoLockParser"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public RustCargoLockParser(ILogger<RustCargoLockParser> logger) => this.logger = logger;

    /// <summary>
    /// Parses a Cargo.lock file and records components.
    /// </summary>
    /// <param name="componentStream">The component stream containing the Cargo.lock file.</param>
    /// <param name="singleFileComponentRecorder">The component recorder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lockfile version, or null if parsing failed.</returns>
    public async Task<int?> ParseAsync(
        IComponentStream componentStream,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(componentStream.Stream);
            var content = await reader.ReadToEndAsync(cancellationToken);
            var cargoLock = Toml.ToModel<CargoLock>(content, options: TomlOptions);
            this.ProcessCargoLock(cargoLock, singleFileComponentRecorder, componentStream);
            return cargoLock.Version;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Failed to parse Cargo.lock file '{CargoLockLocation}'", componentStream.Location);
            return null;
        }
    }

    private static bool IsLocalPackage(CargoPackage package) => package.Source == null;

    private void ProcessCargoLock(CargoLock cargoLock, ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream cargoLockFile)
    {
        try
        {
            var seenAsDependency = new HashSet<CargoPackage>();

            // Pass 1: Create typed components and allow lookup by name.
            var packagesByName = new Dictionary<string, List<(CargoPackage Package, CargoComponent Component)>>();
            if (cargoLock.Package != null)
            {
                foreach (var cargoPackage in cargoLock.Package)
                {
                    // Get or create the list of packages with this name
                    if (!packagesByName.TryGetValue(cargoPackage.Name, out var packageList))
                    {
                        // First package with this name
                        packageList = [];
                        packagesByName.Add(cargoPackage.Name, packageList);
                    }
                    else if (packageList.Any(p => p.Package.Equals(cargoPackage)))
                    {
                        // Ignore duplicate packages
                        continue;
                    }

                    // Create a node for each non-local package to allow adding dependencies later.
                    CargoComponent cargoComponent = null;
                    if (cargoPackage.Source != null)
                    {
                        cargoComponent = new CargoComponent(cargoPackage.Name, cargoPackage.Version);
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
                        if (parentPackage.Dependencies == null)
                        {
                            // This package has no dependency edges to contribute.
                            continue;
                        }

                        // Process each dependency
                        foreach (var dependency in parentPackage.Dependencies)
                        {
                            this.ProcessDependency(
                                cargoLockFile,
                                singleFileComponentRecorder,
                                seenAsDependency,
                                packagesByName,
                                parentPackage,
                                parentComponent,
                                dependency);
                        }
                    }
                }

                // Pass 3: Conservatively mark packages we found no dependency to as roots
                foreach (var packageList in packagesByName.Values)
                {
                    // Get the package and component.
                    foreach (var (package, component) in packageList)
                    {
                        if (package.Source != null && !seenAsDependency.Contains(package))
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
            this.logger.LogError(e, "Failed to process Cargo.lock file '{CargoLockLocation}'", cargoLockFile.Location);
        }
    }

    private void ProcessDependency(
        IComponentStream cargoLockFile,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        HashSet<CargoPackage> seenAsDependency,
        Dictionary<string, List<(CargoPackage Package, CargoComponent Component)>> packagesByName,
        CargoPackage parentPackage,
        CargoComponent parentComponent,
        string dependency)
    {
        try
        {
            if (!this.ParseDependency(dependency, out var childName, out var childVersion, out var childSource))
            {
                throw new FormatException($"Failed to parse dependency '{dependency}'");
            }

            if (!packagesByName.TryGetValue(childName, out var candidatePackages))
            {
                throw new FormatException($"Could not find any package named '{childName}' for dependency string '{dependency}'");
            }

            // Search through the list of candidates to find a match (note that version and source are optional).
            CargoPackage childPackage = null;
            CargoComponent childComponent = null;
            foreach (var (candidatePackage, candidateComponent) in candidatePackages)
            {
                if (childVersion != null && candidatePackage.Version != childVersion)
                {
                    // This does not have the requested version
                    continue;
                }

                if (childSource != null && candidatePackage.Source != childSource)
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
                // This is a dependency on a package without a source
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
            record.PackageInfo = $"{parentPackage.Name}, {parentPackage.Version}, {parentPackage.Source}";
            record.Dependencies = dependency;
            this.logger.LogError(e, "Failed to process dependency in Cargo.lock file '{CargoLockLocation}'", cargoLockFile.Location);
            singleFileComponentRecorder.RegisterPackageParseFailure(record.PackageInfo);
        }
    }

    private bool ParseDependency(string dependency, out string packageName, out string version, out string source)
    {
        var match = DependencyFormatRegex.Match(dependency);
        var packageNameMatch = match.Groups["packageName"];
        var versionMatch = match.Groups["version"];
        var sourceMatch = match.Groups["source"];

        packageName = packageNameMatch.Success ? packageNameMatch.Value : null;
        version = versionMatch.Success ? versionMatch.Value : null;
        source = sourceMatch.Success ? sourceMatch.Value : null;

        if (string.IsNullOrWhiteSpace(source))
        {
            source = null;
        }

        return match.Success;
    }
}

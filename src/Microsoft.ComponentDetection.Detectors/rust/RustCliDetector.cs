namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tomlyn;

/// <summary>
/// A Rust CLI detector that uses the cargo metadata command to detect Rust components.
/// </summary>
public class RustCliDetector : FileComponentDetector, IExperimentalDetector
{
    ////  PkgName[ Version][ (Source)]
    private static readonly Regex DependencyFormatRegex = new Regex(
        @"^(?<packageName>[^ ]+)(?: (?<version>[^ ]+))?(?: \((?<source>[^()]*)\))?$",
        RegexOptions.Compiled);

    private readonly ICommandLineInvocationService cliService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustCliDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The component stream enumerable factory.</param>
    /// <param name="walkerFactory">The walker factory.</param>
    /// <param name="cliService">The command line invocation service.</param>
    /// <param name="logger">The logger.</param>
    public RustCliDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService cliService,
        ILogger<RustCliDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.cliService = cliService;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override string Id => "RustCli";

    /// <inheritdoc />
    public override IEnumerable<string> Categories { get; } = new[] { "Rust" };

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Cargo };

    /// <inheritdoc />
    public override int Version => 2;

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = new[] { "Cargo.toml" };

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var componentStream = processRequest.ComponentStream;
        this.Logger.LogInformation("Discovered Cargo.toml: {Location}", componentStream.Location);
        using var record = new RustGraphTelemetryRecord();
        record.CargoTomlLocation = processRequest.ComponentStream.Location;

        try
        {
            if (!await this.cliService.CanCommandBeLocatedAsync("cargo", null))
            {
                this.Logger.LogWarning("Could not locate cargo command. Skipping Rust CLI detection");
                record.DidRustCliCommandFail = true;
                record.WasRustFallbackStrategyUsed = true;
                record.FallbackReason = "Could not locate cargo command";
            }
            else
            {
                // Use --all-features to ensure that even optional feature dependencies are detected.
                var cliResult = await this.cliService.ExecuteCommandAsync(
                    "cargo",
                    null,
                    "metadata",
                    "--all-features",
                    "--manifest-path",
                    componentStream.Location,
                    "--format-version=1",
                    "--locked");

                if (cliResult.ExitCode != 0)
                {
                    this.Logger.LogWarning("`cargo metadata` failed with {Location}. Ensure the Cargo.lock is up to date.", processRequest.ComponentStream.Location);
                    record.DidRustCliCommandFail = true;
                    record.WasRustFallbackStrategyUsed = true;
                    record.RustCliCommandError = cliResult.StdErr;
                    record.FallbackReason = "`cargo metadata` failed";
                }

                if (!record.DidRustCliCommandFail)
                {
                    var metadata = CargoMetadata.FromJson(cliResult.StdOut);
                    var graph = BuildGraph(metadata);

                    var packages = metadata.Packages.ToDictionary(
                        x => $"{x.Name} {x.Version}",
                        x => (
                            (x.Authors == null || x.Authors.Any(a => string.IsNullOrWhiteSpace(a)) || !x.Authors.Any()) ? null : string.Join(", ", x.Authors),
                            string.IsNullOrWhiteSpace(x.License) ? null : x.License));

                    var root = metadata.Resolve.Root;

                    // A cargo.toml can be used to declare a workspace and not a package (A Virtual Manifest).
                    // In this case, the root will be null as it will not be pulling in dependencies itself.
                    // https://doc.rust-lang.org/cargo/reference/workspaces.html#virtual-workspace
                    if (root == null)
                    {
                        this.Logger.LogWarning("Virtual Manifest: {Location}, falling back to cargo.lock parsing", processRequest.ComponentStream.Location);
                        record.DidRustCliCommandFail = true;
                        record.WasRustFallbackStrategyUsed = true;
                        record.FallbackReason = "Virtual Manifest";
                    }

                    HashSet<string> visitedDependencies = new();
                    if (!record.WasRustFallbackStrategyUsed)
                    {
                        this.TraverseAndRecordComponents(processRequest.SingleFileComponentRecorder, componentStream.Location, graph, root, null, null, packages, visitedDependencies);
                    }
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Failed attempting to call `cargo` with file: {Location}", processRequest.ComponentStream.Location);
            record.DidRustCliCommandFail = true;
            record.RustCliCommandError = e.Message;
            record.WasRustFallbackStrategyUsed = true;
            record.FallbackReason = "InvalidOperationException";
        }
        finally
        {
            if (record.WasRustFallbackStrategyUsed)
            {
                try
                {
                    await this.ProcessCargoLockFallbackAsync(componentStream, processRequest.SingleFileComponentRecorder, record);
                }
                catch (ArgumentException e)
                {
                    this.Logger.LogWarning(e, "fallback failed for {Location}", processRequest.ComponentStream.Location);
                    record.DidRustCliCommandFail = true;
                    record.RustCliCommandError = e.Message;
                    record.WasRustFallbackStrategyUsed = true;
                }

                this.AdditionalProperties.Add(("Rust Fallback", JsonConvert.SerializeObject(record)));
            }
        }
    }

    private static Dictionary<string, Node> BuildGraph(CargoMetadata cargoMetadata) => cargoMetadata.Resolve.Nodes.ToDictionary(x => x.Id);

    private static (string Name, string Version) ParseNameAndVersion(string nameAndVersion)
    {
        var parts = nameAndVersion.Split(' ');
        return (parts[0], parts[1]);
    }

    private static bool IsLocalPackage(CargoPackage package) => package.Source == null;

    private static bool ParseDependency(string dependency, out string packageName, out string version, out string source)
    {
        var match = DependencyFormatRegex.Match(dependency);
        var packageNameMatch = match.Groups["packageName"];
        var versionMatch = match.Groups["version"];
        var sourceMatch = match.Groups["source"];

        packageName = packageNameMatch.Success ? packageNameMatch.Value : null;
        version = versionMatch.Success ? versionMatch.Value : null;
        source = sourceMatch.Success ? sourceMatch.Value : null;

        if (string.IsNullOrEmpty(source))
        {
            source = null;
        }

        return match.Success;
    }

    private void TraverseAndRecordComponents(
        ISingleFileComponentRecorder recorder,
        string location,
        IReadOnlyDictionary<string, Node> graph,
        string id,
        DetectedComponent parent,
        Dep depInfo,
        IReadOnlyDictionary<string, (string Authors, string License)> packagesMetadata,
        ISet<string> visitedDependencies,
        bool explicitlyReferencedDependency = false)
    {
        try
        {
            var isDevelopmentDependency = depInfo?.DepKinds.Any(x => x.Kind is Kind.Dev) ?? false;
            var (name, version) = ParseNameAndVersion(id);

            var (authors, license) = packagesMetadata.TryGetValue($"{name} {version}", out var package)
                ? package
                : (null, null);

            var detectedComponent = new DetectedComponent(new CargoComponent(name, version, authors, license));

            recorder.RegisterUsage(
                detectedComponent,
                explicitlyReferencedDependency,
                isDevelopmentDependency: isDevelopmentDependency,
                parentComponentId: parent?.Component.Id);

            if (!graph.TryGetValue(id, out var node))
            {
                this.Logger.LogWarning("Could not find {Id} at {Location} in cargo metadata output", id, location);
                return;
            }

            foreach (var dep in node.Deps)
            {
                var componentKey = $"{detectedComponent.Component.Id}{dep.Pkg}";
                if (!visitedDependencies.Contains(componentKey))
                {
                    visitedDependencies.Add(componentKey);
                    this.TraverseAndRecordComponents(recorder, location, graph, dep.Pkg, detectedComponent, dep, packagesMetadata, visitedDependencies, parent == null);
                }
            }
        }
        catch (IndexOutOfRangeException e)
        {
            this.Logger.LogWarning(e, "Could not parse {Id} at {Location}", id, location);
            recorder.RegisterPackageParseFailure(id);
        }
    }

    private IComponentStream FindCorrespondingCargoLock(IComponentStream cargoToml, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var cargoLockLocation = Path.Combine(Path.GetDirectoryName(cargoToml.Location), "Cargo.lock");
        var cargoLockStream = this.ComponentStreamEnumerableFactory.GetComponentStreams(new FileInfo(cargoToml.Location).Directory, new List<string> { "Cargo.lock" }, (name, directoryName) => false, recursivelyScanDirectories: false).FirstOrDefault();
        if (cargoLockStream == null)
        {
            return null;
        }

        if (cargoLockStream.Stream.CanRead)
        {
            return cargoLockStream;
        }
        else
        {
            using var fileStream = new FileStream(cargoLockStream.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new ComponentStream()
            {
                Location = cargoLockStream.Location,
                Pattern = cargoLockStream.Pattern,
                Stream = fileStream,
            };
        }
    }

    private async Task ProcessCargoLockFallbackAsync(IComponentStream cargoTomlFile, ISingleFileComponentRecorder singleFileComponentRecorder, RustGraphTelemetryRecord record)
    {
        var cargoLockFileStream = this.FindCorrespondingCargoLock(cargoTomlFile, singleFileComponentRecorder);
        if (cargoLockFileStream == null)
        {
            this.Logger.LogWarning("Could not find Cargo.lock file for {CargoTomlLocation}, skipping processing", cargoTomlFile.Location);
            record.FallbackCargoLockFound = false;
            return;
        }

        record.FallbackCargoLockLocation = cargoLockFileStream.Location;
        record.FallbackCargoLockFound = true;
        using var reader = new StreamReader(cargoLockFileStream.Stream);
        var content = await reader.ReadToEndAsync();
        var options = new TomlModelOptions
        {
            IgnoreMissingProperties = true,
        };
        var cargoLock = Toml.ToModel<CargoLock>(content, options: options);
        this.RecordLockfileVersion(cargoLock.Version);
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
                        packageList = new List<(CargoPackage, CargoComponent)>();
                        packagesByName.Add(cargoPackage.Name, packageList);
                    }
                    else if (packageList.Any(p => p.Package.Equals(cargoPackage)))
                    {
                        // Ignore duplicate packages
                        continue;
                    }

                    // Create a node for each non-local package to allow adding dependencies later.
                    CargoComponent cargoComponent = null;
                    if (!IsLocalPackage(cargoPackage))
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
                            this.ProcessDependency(cargoLockFileStream, singleFileComponentRecorder, seenAsDependency, packagesByName, parentPackage, parentComponent, dependency);
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
            this.Logger.LogError(e, "Failed to process Cargo.lock file '{CargoLockLocation}'", cargoLockFileStream.Location);
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
            // Extract the information from the dependency (name with optional version and source)
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

            this.Logger.LogError(e, "Failed to process Cargo.lock file '{CargoLockLocation}'", cargoLockFile.Location);
            singleFileComponentRecorder.RegisterPackageParseFailure(record.PackageInfo);
        }
    }
}

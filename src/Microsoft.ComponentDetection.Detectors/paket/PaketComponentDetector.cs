namespace Microsoft.ComponentDetection.Detectors.Paket;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects NuGet packages in paket.lock files.
/// Paket is a dependency manager for .NET that provides better control over package dependencies.
/// </summary>
// TODO: Promote to default-on (remove IDefaultOffComponentDetector) once validated in real-world usage.
public sealed class PaketComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private static readonly Regex PackageLineRegex = new(@"^\s{4}(\S+)\s+\(([^\)]+)\)", RegexOptions.Compiled);
    private static readonly Regex DependencyLineRegex = new(@"^\s{6}(\S+)\s+\(([^)]+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="PaketComponentDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The factory for handing back component streams to File detectors.</param>
    /// <param name="walkerFactory">The factory for creating directory walkers.</param>
    /// <param name="logger">The logger to use.</param>
    public PaketComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<PaketComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override IList<string> SearchPatterns => ["paket.lock"];

    /// <inheritdoc />
    public override string Id => "Paket";

    /// <inheritdoc />
    public override IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)!];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.NuGet];

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            using var reader = new StreamReader(processRequest.ComponentStream.Stream);

            // First pass: collect all resolved packages and their dependency relationships.
            // In paket.lock, 4-space indented lines are resolved packages with pinned versions.
            // 6-space indented lines are dependency specifications (version constraints) of the parent
            // package; they are NOT resolved versions. The actual resolved version for each dependency
            // will appear as its own 4-space entry elsewhere in the file.
            // Limitation: without cross-referencing paket.dependencies, we cannot perfectly distinguish
            // between direct and transitive dependencies. We initially register all 4-space resolved packages,
            // then use the dependency graph to approximate: packages that appear as dependencies of other
            // packages are marked as transitive, and the rest are treated as explicit.
            var resolvedPackages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dependencyRelationships = new List<(string ParentName, string DependencyName)>();

            var currentSection = string.Empty;
            string? currentPackageName = null;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Check if this is a section header (e.g., NUGET, GITHUB, HTTP, GROUP)
                if (!line.StartsWith(' ') && line.Trim().Length > 0)
                {
                    currentSection = line.Trim();
                    currentPackageName = null;
                    continue;
                }

                // Only process NUGET section for now
                if (!currentSection.Equals("NUGET", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if this is a remote line (source URL)
                if (line.TrimStart().StartsWith("remote:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if this is a package line (4 spaces indentation) - these are resolved packages
                var packageMatch = PackageLineRegex.Match(line);
                if (packageMatch.Success)
                {
                    currentPackageName = packageMatch.Groups[1].Value;
                    var currentPackageVersion = packageMatch.Groups[2].Value;

                    // TryAdd keeps the first occurrence. If the same package appears in multiple GROUPs
                    // with different versions, only the first is registered. This is a known simplification;
                    // full GROUP-aware tracking could be added in a future iteration.
                    if (!resolvedPackages.TryAdd(currentPackageName, currentPackageVersion))
                    {
                        this.Logger.LogDebug(
                            "Duplicate package {PackageName} found with version {Version}; keeping previously resolved version {ExistingVersion}",
                            currentPackageName,
                            currentPackageVersion,
                            resolvedPackages[currentPackageName]);
                    }
                    continue;
                }

                // Check if this is a dependency line (6 spaces indentation) - these are version constraints
                var dependencyMatch = DependencyLineRegex.Match(line);
                if (dependencyMatch.Success && currentPackageName != null)
                {
                    var dependencyName = dependencyMatch.Groups[1].Value;
                    dependencyRelationships.Add((currentPackageName, dependencyName));
                }
            }

            // Build a set of package names that appear as dependencies of other packages
            var transitiveDependencyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, dependencyName) in dependencyRelationships)
            {
                transitiveDependencyNames.Add(dependencyName);
            }

            // Register all resolved packages
            foreach (var (name, version) in resolvedPackages)
            {
                var component = new DetectedComponent(new NuGetComponent(name, version));
                singleFileComponentRecorder.RegisterUsage(
                    component,
                    isExplicitReferencedDependency: !transitiveDependencyNames.Contains(name));
            }

            // Register parent-child relationships using the dependency specifications
            foreach (var (parentName, dependencyName) in dependencyRelationships)
            {
                if (resolvedPackages.ContainsKey(dependencyName) && resolvedPackages.ContainsKey(parentName))
                {
                    var parentVersion = resolvedPackages[parentName];
                    var parentComponentId = new NuGetComponent(parentName, parentVersion).Id;

                    var depVersion = resolvedPackages[dependencyName];
                    var depComponent = new DetectedComponent(new NuGetComponent(dependencyName, depVersion));

                    singleFileComponentRecorder.RegisterUsage(
                        depComponent,
                        isExplicitReferencedDependency: false,
                        parentComponentId: parentComponentId);
                }
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            singleFileComponentRecorder.RegisterPackageParseFailure(processRequest.ComponentStream.Location);
            this.Logger.LogWarning(e, "Failed to read paket.lock file {File}", processRequest.ComponentStream.Location);
            processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(processRequest.ComponentStream.Location);
        }
    }
}

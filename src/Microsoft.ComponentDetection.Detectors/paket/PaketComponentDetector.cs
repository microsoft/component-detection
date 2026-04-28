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
    /// Well-known Paket group names that indicate development-time dependencies.
    /// Exact matches (case-insensitive): test, tests, docs, documentation, build, analyzers, fake,
    /// benchmark, benchmarks, samples, designtime.
    /// Suffix matches (case-insensitive): groups ending with "test" or "tests" to cover names like
    /// "unittest", "unittests", "integrationtest", "integrationtests", etc.
    /// </summary>
    private static readonly HashSet<string> ExactDevGroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "test", "tests", "docs", "documentation", "build", "analyzers", "fake",
        "benchmark", "benchmarks", "samples", "designtime",
    };

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
    public override int Version => 2;

    /// <summary>
    /// Determines whether a Paket group name represents a development-time dependency group.
    /// The unnamed/default group and "Main" are considered production groups.
    /// </summary>
    /// <param name="groupName">The group name from the paket.lock file, or empty string for the default group.</param>
    /// <returns><c>true</c> if the group is a well-known development group; <c>false</c> otherwise.</returns>
    internal static bool IsDevelopmentDependencyGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName) || groupName.Equals("Main", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ExactDevGroupNames.Contains(groupName))
        {
            return true;
        }

        // Suffix matches: *test, *tests (e.g., UnitTest, IntegrationTests)
        if (groupName.EndsWith("test", StringComparison.OrdinalIgnoreCase) ||
            groupName.EndsWith("tests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            using var reader = new StreamReader(processRequest.ComponentStream.Stream);

            // First pass: collect all resolved packages and their dependency relationships, keyed by group.
            // In paket.lock, 4-space indented lines are resolved packages with pinned versions.
            // 6-space indented lines are dependency specifications (version constraints) of the parent
            // package; they are NOT resolved versions. The actual resolved version for each dependency
            // will appear as its own 4-space entry elsewhere in the file.
            //
            // Packages are tracked per group because the same package may appear in multiple groups
            // (e.g., FSharp.Core in both "Build" and "Server") potentially with different versions.
            // Group names are also used to classify packages as development dependencies: well-known
            // group names like "Test", "Build", "Docs", etc. indicate development-time dependencies.
            //
            // Limitation: without cross-referencing paket.dependencies or paket.references, we cannot
            // perfectly distinguish between direct and transitive dependencies. We use the dependency
            // graph within each group to approximate: packages that appear as dependencies of other
            // packages are marked as transitive, and the rest are treated as explicit.

            // Key: (groupName, packageName) -> version
            var resolvedPackages = new Dictionary<(string Group, string Name), string>(GroupAndNameComparer.Instance);

            // (groupName, parentName, dependencyName)
            var dependencyRelationships = new List<(string Group, string ParentName, string DependencyName)>();

            var currentSection = string.Empty;
            var currentGroupName = string.Empty; // empty string = default/unnamed group
            string? currentPackageName = null;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Check if this is a section header (e.g., NUGET, GITHUB, HTTP, GROUP, RESTRICTION, STORAGE)
                if (!line.StartsWith(' ') && line.Trim().Length > 0)
                {
                    var trimmed = line.Trim();

                    // GROUP lines set the current group context; they are not a "section" like NUGET.
                    // The format is "GROUP <name>" and subsequent sections (NUGET, GITHUB, etc.)
                    // belong to this group until the next GROUP line.
                    if (trimmed.StartsWith("GROUP ", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 6)
                    {
                        currentGroupName = trimmed[6..].Trim();
                        currentSection = string.Empty;
                        currentPackageName = null;
                    }
                    else
                    {
                        currentSection = trimmed;
                        currentPackageName = null;
                    }

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

                    var key = (currentGroupName, currentPackageName);
                    if (!resolvedPackages.TryAdd(key, currentPackageVersion))
                    {
                        this.Logger.LogDebug(
                            "Duplicate package {PackageName} found in group '{GroupName}' with version {Version}; keeping previously resolved version {ExistingVersion}",
                            currentPackageName,
                            currentGroupName,
                            currentPackageVersion,
                            resolvedPackages[key]);
                    }

                    continue;
                }

                // Check if this is a dependency line (6 spaces indentation) - these are version constraints
                var dependencyMatch = DependencyLineRegex.Match(line);
                if (dependencyMatch.Success && currentPackageName != null)
                {
                    var dependencyName = dependencyMatch.Groups[1].Value;
                    dependencyRelationships.Add((currentGroupName, currentPackageName, dependencyName));
                }
            }

            // Build a set of package names (per group) that appear as dependencies of other packages
            var transitiveDependencyNames = new HashSet<(string Group, string Name)>(GroupAndNameComparer.Instance);
            foreach (var (group, _, dependencyName) in dependencyRelationships)
            {
                transitiveDependencyNames.Add((group, dependencyName));
            }

            // Register all resolved packages with group-aware isDevelopmentDependency.
            // If a package appears in multiple groups, it will be registered multiple times with
            // potentially different isDevelopmentDependency values. The framework's AND-merge
            // semantics ensure that if ANY registration says false (production), the final result
            // is false -- preventing accidental hiding of production dependencies.
            foreach (var ((group, name), version) in resolvedPackages)
            {
                var isDev = IsDevelopmentDependencyGroup(group);
                var component = new DetectedComponent(new NuGetComponent(name, version));
                singleFileComponentRecorder.RegisterUsage(
                    component,
                    isExplicitReferencedDependency: !transitiveDependencyNames.Contains((group, name)),
                    isDevelopmentDependency: isDev);
            }

            // Register parent-child relationships using the dependency specifications
            foreach (var (group, parentName, dependencyName) in dependencyRelationships)
            {
                var parentKey = (group, parentName);
                var depKey = (group, dependencyName);

                if (resolvedPackages.ContainsKey(depKey) && resolvedPackages.ContainsKey(parentKey))
                {
                    var isDev = IsDevelopmentDependencyGroup(group);
                    var parentVersion = resolvedPackages[parentKey];
                    var parentComponentId = new NuGetComponent(parentName, parentVersion).Id;

                    var depVersion = resolvedPackages[depKey];
                    var depComponent = new DetectedComponent(new NuGetComponent(dependencyName, depVersion));

                    singleFileComponentRecorder.RegisterUsage(
                        depComponent,
                        isExplicitReferencedDependency: false,
                        parentComponentId: parentComponentId,
                        isDevelopmentDependency: isDev);
                }
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(processRequest.ComponentStream.Location);
            this.Logger.LogWarning(e, "Failed to read paket.lock file {File}", processRequest.ComponentStream.Location);
        }
    }

    /// <summary>
    /// Case-insensitive equality comparer for (Group, Name) tuples used as dictionary keys.
    /// </summary>
    private sealed class GroupAndNameComparer : IEqualityComparer<(string Group, string Name)>
    {
        public static readonly GroupAndNameComparer Instance = new();

        public bool Equals((string Group, string Name) x, (string Group, string Name) y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Group, y.Group) &&
                   StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);
        }

        public int GetHashCode((string Group, string Name) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Group),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
        }
    }
}

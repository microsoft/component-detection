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
    /// <summary>
    /// The detector id, exposed so other detectors (e.g. NuGet) can defer paket.lock handling
    /// to this detector when it has been explicitly enabled.
    /// </summary>
    public const string DetectorId = "Paket";

    /// <summary>
    /// The companion file that declares the direct (top-level) dependencies for a Paket setup.
    /// When present next to paket.lock it lets us classify direct vs. transitive dependencies precisely.
    /// </summary>
    public const string DependenciesFileName = "paket.dependencies";

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

    private readonly IFileUtilityService fileUtilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaketComponentDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The factory for handing back component streams to File detectors.</param>
    /// <param name="walkerFactory">The factory for creating directory walkers.</param>
    /// <param name="fileUtilityService">The service used to read the companion paket.dependencies file.</param>
    /// <param name="logger">The logger to use.</param>
    public PaketComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IFileUtilityService fileUtilityService,
        ILogger<PaketComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override IList<string> SearchPatterns => ["paket.lock"];

    /// <inheritdoc />
    public override string Id => DetectorId;

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

    /// <summary>
    /// Normalizes a Paket group name so the default group is represented consistently. Paket's default
    /// group can be written as the unnamed group (empty) or explicitly as "Main"; both are treated as the
    /// same group so declarations in paket.dependencies line up with entries in paket.lock.
    /// </summary>
    /// <param name="groupName">The raw group name, or empty for the default group.</param>
    /// <returns>An empty string for the default/Main group; otherwise the original group name.</returns>
    internal static string NormalizeGroupName(string? groupName) =>
        string.IsNullOrEmpty(groupName) || groupName.Equals("Main", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : groupName;

    /// <summary>
    /// Parses the direct (top-level) dependencies declared in a paket.dependencies file, keyed by group.
    /// Only <c>nuget</c> declarations are considered; <c>github</c>, <c>git</c>, <c>http</c>, source and
    /// option lines are ignored. Returns <c>null</c> when no NuGet declarations are found so callers can
    /// fall back to the lock-graph heuristic.
    /// </summary>
    /// <param name="content">The contents of the paket.dependencies file.</param>
    /// <returns>The set of declared direct dependencies, or <c>null</c> if none were found.</returns>
    internal static HashSet<(string Group, string Name)>? ParseDeclaredDirectDependencies(string content)
    {
        var declared = new HashSet<(string Group, string Name)>(GroupAndNameComparer.Instance);
        var currentGroup = string.Empty;

        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith("group ", StringComparison.OrdinalIgnoreCase))
            {
                currentGroup = NormalizeGroupName(trimmed[6..].Trim());
                continue;
            }

            if (trimmed.StartsWith("nuget ", StringComparison.OrdinalIgnoreCase))
            {
                // Format: "nuget <PackageName> [version/constraint] [options]" - the name is the first token.
                var tokens = trimmed[6..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                {
                    declared.Add((currentGroup, tokens[0]));
                }
            }
        }

        return declared.Count > 0 ? declared : null;
    }

    /// <summary>
    /// Reads and parses the paket.dependencies file sitting next to the given paket.lock, if present.
    /// Any IO/parse failure is swallowed (and logged) so direct/transitive classification can safely fall
    /// back to the lock-graph heuristic.
    /// </summary>
    /// <param name="lockFileLocation">The full path to the paket.lock file being processed.</param>
    /// <returns>The declared direct dependencies, or <c>null</c> when unavailable.</returns>
    private HashSet<(string Group, string Name)>? TryReadDeclaredDirectDependencies(string lockFileLocation)
    {
        try
        {
            var directory = Path.GetDirectoryName(lockFileLocation);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var dependenciesPath = Path.Combine(directory, DependenciesFileName);
            if (this.fileUtilityService?.Exists(dependenciesPath) != true)
            {
                return null;
            }

            var content = this.fileUtilityService.ReadAllText(dependenciesPath);
            return string.IsNullOrEmpty(content) ? null : ParseDeclaredDirectDependencies(content);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            this.Logger.LogWarning(
                e,
                "Failed to read {DependenciesFile} next to {LockFile}; falling back to the lock-graph heuristic for direct/transitive classification.",
                DependenciesFileName,
                lockFileLocation);
            return null;
        }
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
            // Direct vs. transitive classification: when a companion paket.dependencies file is present
            // next to paket.lock, it lists the direct (top-level) dependencies that were explicitly
            // declared, so we use it as the authoritative source. When it is absent (or unreadable) we
            // fall back to a graph heuristic: packages that appear as a dependency of another package
            // within the same group are treated as transitive, and the rest as explicit. The heuristic
            // cannot perfectly distinguish a direct dependency that is also pulled in transitively.

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

                    // The version capture group can match whitespace-only content (e.g. a malformed
                    // "Foo (   )" line). NuGetComponent requires a non-empty version, so skip such entries
                    // here instead of letting the constructor throw later and abort the whole file.
                    if (string.IsNullOrWhiteSpace(currentPackageVersion))
                    {
                        this.Logger.LogWarning(
                            "Skipping paket package {PackageName} in group '{GroupName}' because it has no resolved version.",
                            currentPackageName,
                            currentGroupName);
                        singleFileComponentRecorder.RegisterPackageParseFailure(processRequest.ComponentStream.Location);
                        currentPackageName = null;
                        continue;
                    }

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

            // Prefer the explicit declarations from the companion paket.dependencies file when available.
            var declaredDirectDependencies = this.TryReadDeclaredDirectDependencies(processRequest.ComponentStream.Location);

            // Register all resolved packages with group-aware isDevelopmentDependency.
            // If a package appears in multiple groups, it will be registered multiple times with
            // potentially different isDevelopmentDependency values. The framework's AND-merge
            // semantics ensure that if ANY registration says false (production), the final result
            // is false -- preventing accidental hiding of production dependencies.
            foreach (var ((group, name), version) in resolvedPackages)
            {
                var isDev = IsDevelopmentDependencyGroup(group);
                var isExplicit = declaredDirectDependencies != null
                    ? declaredDirectDependencies.Contains((NormalizeGroupName(group), name))
                    : !transitiveDependencyNames.Contains((group, name));
                var component = new DetectedComponent(new NuGetComponent(name, version));
                singleFileComponentRecorder.RegisterUsage(
                    component,
                    isExplicitReferencedDependency: isExplicit,
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
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Catch all parsing/IO exceptions (e.g. a malformed line that yields an empty version and makes
            // NuGetComponent throw) so a single bad paket.lock cannot fault the detector and fail the whole
            // scan. Cancellation is intentionally allowed to propagate.
            processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(processRequest.ComponentStream.Location);
            this.Logger.LogWarning(e, "Failed to process paket.lock file {File}", processRequest.ComponentStream.Location);
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

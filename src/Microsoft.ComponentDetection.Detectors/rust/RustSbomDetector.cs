namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using global::DotNet.Globbing;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

/// <summary>
/// A unified Rust detector that orchestrates SBOM, CLI, and Crate parsing.
/// </summary>
public class RustSbomDetector : FileComponentDetector
{
    private static readonly TomlModelOptions TomlOptions = new TomlModelOptions
    {
        IgnoreMissingProperties = true,
    };

    private readonly IPathUtilityService pathUtilityService;
    private readonly RustSbomParser sbomParser;
    private readonly IRustCliParser cliParser;
    private readonly RustCargoLockParser cargoLockParser;
    private readonly IRustMetadataContextBuilder metadataContextBuilder;

    private readonly HashSet<string> visitedDirs;
    private readonly List<GlobRule> visitedGlobRules;
    private readonly StringComparer pathComparer;
    private readonly StringComparison pathComparison;
    private IReadOnlyDictionary<string, HashSet<string>> ownershipMap;
    private Dictionary<string, Contracts.CargoMetadata> manifestMetadataCache;
    private DetectionMode mode;

    public RustSbomDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService cliService,
        IEnvironmentVariableService envVarService,
        ILogger<RustSbomDetector> logger,
        IRustMetadataContextBuilder metadataContextBuilder,
        IPathUtilityService pathUtilityService,
        IRustCliParser cliParser)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
        this.pathUtilityService = pathUtilityService;

        // Initialize parsers
        this.sbomParser = new RustSbomParser(logger);
        this.cliParser = cliParser;
        this.cargoLockParser = new RustCargoLockParser(logger);
        this.metadataContextBuilder = metadataContextBuilder;

        // Initialize with uniform case-insensitive comparison across all platforms
        this.pathComparer = StringComparer.OrdinalIgnoreCase;
        this.pathComparison = StringComparison.OrdinalIgnoreCase;
        this.visitedDirs = new HashSet<string>(this.pathComparer);
        this.visitedGlobRules = [];
        this.manifestMetadataCache = new Dictionary<string, Contracts.CargoMetadata>(this.pathComparer);
    }

    /// <summary>
    /// Detection modes for the unified Rust detector.
    /// </summary>
    private enum DetectionMode
    {
        /// <summary>
        /// Only use SBOM files for detection.
        /// </summary>
        SBOM_ONLY,

        /// <summary>
        /// Use fallback strategy (Cargo CLI and/or Cargo.lock parsing).
        /// </summary>
        FALLBACK,
    }

    /// <summary>
    /// File kinds for skip logic.
    /// </summary>
    private enum FileKind
    {
        /// <summary>
        /// Cargo.toml file.
        /// </summary>
        CargoToml,

        /// <summary>
        /// Cargo.lock file.
        /// </summary>
        CargoLock,

        /// <summary>
        /// Cargo SBOM file.
        /// </summary>
        CargoSbom,
    }

    /// <inheritdoc />
    public override string Id => nameof(RustSbomDetector);

    /// <inheritdoc />
    public override IEnumerable<string> Categories { get; } = ["Rust"];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Cargo];

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = ["Cargo.toml", "Cargo.lock", "*.cargo-sbom.json"];

    /// <inheritdoc />
    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("Preparing Rust component detection");

        // Step 1: Collect all process requests into a list
        var allRequests = await processRequests.ToList().ToTask(cancellationToken);

        // Step 2: Determine detection mode
        var hasSbomFiles = allRequests.Any(r => r.ComponentStream.Location.EndsWith(".cargo-sbom.json", this.pathComparison));
        this.mode = hasSbomFiles ? DetectionMode.SBOM_ONLY : DetectionMode.FALLBACK;

        this.Logger.LogInformation("Detection mode: {Mode}", this.mode);

        // Collect Cargo.toml paths ordered (depth, then path)
        var tomlPaths = allRequests
            .Select(r => r.ComponentStream.Location)
            .Where(p => string.Equals(Path.GetFileName(p), "Cargo.toml", this.pathComparison))
            .OrderBy(p => this.GetDirectoryDepth(p))
            .ThenBy(p => p, this.pathComparer)
            .ToList();

        if (tomlPaths.Count > 0)
        {
            try
            {
                this.Logger.LogInformation("Building Rust ownership map from {Count} Cargo.toml files", tomlPaths.Count);
                var ownership = await this.metadataContextBuilder.BuildPackageOwnershipMapAsync(tomlPaths, cancellationToken);
                this.ownershipMap = ownership.PackageToTomls;
                this.manifestMetadataCache = ownership.ManifestToMetadata;
                this.Logger.LogInformation(
                    "Loaded Rust ownership (packages: {PkgCount}) and metadata cache (manifests: {ManifestCount})",
                    this.ownershipMap?.Count ?? 0,
                    this.manifestMetadataCache?.Count ?? 0);

                if (ownership.FailedManifests?.Count > 0)
                {
                    this.Logger.LogInformation(
                        "Rust metadata failed for {Count} manifests (will rely on lockfiles): {Manifests}",
                        ownership.FailedManifests.Count,
                        string.Join(", ", ownership.FailedManifests));
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to compute Rust ownership/metadata cache; proceeding without cache");
                this.ownershipMap = null;
                this.manifestMetadataCache = null;
            }
        }
        else
        {
            this.Logger.LogInformation("No Cargo.toml files found; ownership and metadata cache unavailable");
            this.ownershipMap = null;
            this.manifestMetadataCache = null;
        }

        IEnumerable<ProcessRequest> filteredRequests;
        if (this.mode == DetectionMode.SBOM_ONLY)
        {
            // Only SBOM files, ordered by path ascending
            filteredRequests = allRequests
                .Where(r => r.ComponentStream.Location.EndsWith(".cargo-sbom.json", this.pathComparison))
                .OrderBy(r => r.ComponentStream.Location, this.pathComparer);

            this.Logger.LogInformation("SBOM_ONLY mode: Processing {Count} SBOM files", filteredRequests.Count());
        }
        else
        {
            // FALLBACK mode: Select Cargo.toml and Cargo.lock files
            // Order: TOML before LOCK, then depth ascending, then path ascending
            filteredRequests = allRequests
                .Where(r =>
                {
                    var fileName = Path.GetFileName(r.ComponentStream.Location);
                    return fileName.Equals("Cargo.toml", this.pathComparison) ||
                           fileName.Equals("Cargo.lock", this.pathComparison);
                })
                .OrderBy(r => Path.GetFileName(r.ComponentStream.Location).Equals("Cargo.lock", this.pathComparison) ? 1 : 0) // TOML before LOCK
                .ThenBy(r => this.GetDirectoryDepth(r.ComponentStream.Location))
                .ThenBy(r => r.ComponentStream.Location, this.pathComparer);
            this.Logger.LogInformation("FALLBACK mode: Processing {Count} Cargo.toml/Cargo.lock files", filteredRequests.Count());
        }

        // Step 4: Return the ordered sequence as an observable
        return filteredRequests.ToObservable();
    }

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        var componentStream = processRequest.ComponentStream;
        var location = componentStream.Location;
        var normLocation = this.pathUtilityService.NormalizePath(location);
        var directory = Path.GetDirectoryName(location);
        var normDirectory = this.pathUtilityService.NormalizePath(directory);
        var fileName = Path.GetFileName(location);

        this.Logger.LogInformation("Processing file: {Location}", normLocation);

        // Determine file kind
        FileKind fileKind;
        if (fileName.Equals("Cargo.toml", this.pathComparison))
        {
            fileKind = FileKind.CargoToml;
        }
        else if (fileName.Equals("Cargo.lock", this.pathComparison))
        {
            fileKind = FileKind.CargoLock;
        }
        else
        {
            fileKind = FileKind.CargoSbom;
        }

        // Check if directory should be skipped
        if (this.ShouldSkip(directory, fileKind, location))
        {
            this.Logger.LogInformation("Skipping file due to skip rules: {Location}", normLocation);
            return;
        }

        if (this.mode == DetectionMode.SBOM_ONLY)
        {
            await this.ProcessSbomFileAsync(processRequest, cancellationToken);
        }
        else
        {
            // FALLBACK mode
            if (fileKind == FileKind.CargoToml)
            {
                await this.ProcessCargoTomlAsync(processRequest, directory, cancellationToken);
            }
            else if (fileKind == FileKind.CargoLock)
            {
                await this.ProcessCargoLockAsync(processRequest, directory, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Calculates the depth of a directory path by counting the number of directory separators.
    /// </summary>
    /// <param name="path">The file or directory path to analyze.</param>
    /// <returns>
    /// The number of directory separators in the normalized path, representing the depth.
    /// Returns 0 if the path is null or empty.
    /// </returns>
    /// <remarks>
    /// The path is normalized to use forward slashes before counting separators.
    /// This ensures consistent depth calculation across different operating systems.
    /// </remarks>
    private int GetDirectoryDepth(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return 0;
        }

        var normalizedPath = this.pathUtilityService.NormalizePath(path);
        return normalizedPath.Count(c => c == Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Determines whether a file should be skipped based on visited directories and workspace glob rules.
    /// </summary>
    /// <param name="directory">The directory path containing the file.</param>
    /// <param name="fileKind">The kind of file being processed (CargoToml, CargoLock, or CargoSbom).</param>
    /// <param name="fullPath">The full path to the file being evaluated.</param>
    /// <returns>
    /// <c>true</c> if the file should be skipped; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The skip logic follows these rules:
    /// <list type="number">
    /// <item>If the directory has already been processed, skip immediately.</item>
    /// <item>Workspace-only Cargo.toml files (with [workspace] but no [package] section) are never skipped.</item>
    /// <item>Files are checked against workspace glob rules for inclusion/exclusion patterns.</item>
    /// </list>
    /// </remarks>
    private bool ShouldSkip(string directory, FileKind fileKind, string fullPath)
    {
        var normalizedDir = this.pathUtilityService.NormalizePath(directory);

        // 1. If directory already processed, skip immediately
        if (this.visitedDirs.Contains(normalizedDir))
        {
            return true;
        }

        // 2. Workspace-only Cargo.toml should always be processed (never skipped)
        if (fileKind == FileKind.CargoToml && this.IsWorkspaceOnlyToml(fullPath))
        {
            return false;
        }

        var normalizedFullPath = this.pathUtilityService.NormalizePath(fullPath);

        // 3. Check each workspace rule for inclusion/exclusion
        foreach (var rule in this.visitedGlobRules)
        {
            if (!this.IsDescendantOf(normalizedDir, rule.Root))
            {
                continue;
            }

            var relativePath = this.GetRelativePath(rule.Root, normalizedDir);

            // Match against include globs
            var matchesInclude = rule.IncludeGlobs.Any(g =>
                g.IsMatch(relativePath) || g.IsMatch(normalizedFullPath));

            if (!matchesInclude)
            {
                continue;
            }

            // Match against exclude globs
            var matchesExclude = rule.ExcludeGlobs.Any(g =>
                g.IsMatch(relativePath) || g.IsMatch(normalizedFullPath));

            if (matchesExclude)
            {
                continue;
            }

            // If included and not excluded, skip this directory
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a glob rule for workspace member filtering based on include and exclude patterns.
    /// </summary>
    /// <param name="root">The root directory path where the workspace is defined.</param>
    /// <param name="includes">Collection of glob patterns to include workspace members (e.g., "member1", "path/*").</param>
    /// <param name="excludes">Collection of glob patterns to exclude workspace members (e.g., "examples/*", "tests/*").</param>
    /// <remarks>
    /// This method normalizes all paths and patterns for cross-platform compatibility.
    /// On Windows, patterns are evaluated case-insensitively, while on other platforms they are case-sensitive.
    /// The glob rule is used to determine whether files in descendant directories should be skipped during detection.
    /// </remarks>
    private void AddGlobRule(string root, IEnumerable<string> includes, IEnumerable<string> excludes)
    {
        var normalizedRoot = this.pathUtilityService.NormalizePath(root);
        var includesList = includes?.ToList() ?? [];
        var excludesList = excludes?.ToList() ?? [];

        var globOptions = new GlobOptions
        {
            Evaluation = new EvaluationOptions
            {
                CaseInsensitive = true,
            },
        };

        var includeGlobs = new List<Glob>();
        foreach (var pattern in includesList)
        {
            var normalizedPattern = this.pathUtilityService.NormalizePath(pattern);
            includeGlobs.Add(Glob.Parse(normalizedPattern, globOptions));
        }

        var excludeGlobs = new List<Glob>();
        foreach (var pattern in excludesList)
        {
            var normalizedPattern = this.pathUtilityService.NormalizePath(pattern);
            excludeGlobs.Add(Glob.Parse(normalizedPattern, globOptions));
        }

        var rule = new GlobRule
        {
            Root = normalizedRoot,
            Includes = includesList,
            Excludes = excludesList,
            IncludeGlobs = includeGlobs,
            ExcludeGlobs = excludeGlobs,
        };

        this.visitedGlobRules.Add(rule);
        this.Logger.LogDebug("Added glob rule with root {Root}, {IncludeCount} includes, {ExcludeCount} excludes", normalizedRoot, includesList.Count, excludesList.Count);
    }

    /// <summary>
    /// Determines if the specified path is a descendant of the potential parent directory.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="potentialParent">The potential parent directory path.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="path"/> is a descendant of or equal to <paramref name="potentialParent"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method normalizes both paths for cross-platform comparison and handles case sensitivity based on the operating system.
    /// The comparison treats paths with and without trailing separators as equivalent.
    /// </remarks>
    private bool IsDescendantOf(string path, string potentialParent)
    {
        var normalizedPath = this.pathUtilityService.NormalizePath(path);
        var normalizedParent = this.pathUtilityService.NormalizePath(potentialParent);

        // Ensure parent path ends with separator for proper comparison
        if (!normalizedParent.EndsWith('/'))
        {
            normalizedParent += "/";
        }

        return normalizedPath.StartsWith(normalizedParent, this.pathComparison) ||
               normalizedPath.Equals(normalizedParent.TrimEnd('/'), this.pathComparison);
    }

    /// <summary>
    /// Calculates the relative path from a base path to a full path.
    /// </summary>
    /// <param name="basePath">The base directory path to calculate relative to.</param>
    /// <param name="fullPath">The full path to convert to a relative path.</param>
    /// <returns>
    /// The relative path from <paramref name="basePath"/> to <paramref name="fullPath"/>.
    /// If <paramref name="fullPath"/> is not under <paramref name="basePath"/>, returns the normalized full path.
    /// </returns>
    /// <remarks>
    /// The method normalizes both paths for cross-platform comparison and handles case sensitivity based on the operating system.
    /// The base path is automatically appended with a trailing separator if not present to ensure correct path comparison.
    /// </remarks>
    private string GetRelativePath(string basePath, string fullPath)
    {
        var normalizedBase = this.pathUtilityService.NormalizePath(basePath);
        var normalizedFull = this.pathUtilityService.NormalizePath(fullPath);

        if (!normalizedBase.EndsWith('/'))
        {
            normalizedBase += "/";
        }

        if (normalizedFull.StartsWith(normalizedBase, this.pathComparison))
        {
            return normalizedFull[normalizedBase.Length..];
        }

        return normalizedFull;
    }

    /// <summary>
    /// Determines whether the specified Cargo.toml file is a workspace-only configuration file.
    /// </summary>
    /// <param name="cargoTomlPath">The full path to the Cargo.toml file to analyze.</param>
    /// <returns>
    /// <c>true</c> if the file contains a [workspace] section but no [package] section, indicating it is a workspace-only file;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// A workspace-only Cargo.toml file defines workspace configuration and members but does not define a package itself.
    /// Such files should always be processed during detection and never skipped, as they provide critical workspace structure information.
    /// If the file cannot be parsed, this method logs a warning and returns <c>false</c> to allow continued processing.
    /// </remarks>
    private bool IsWorkspaceOnlyToml(string cargoTomlPath)
    {
        try
        {
            var content = File.ReadAllText(cargoTomlPath);
            var tomlTable = Toml.ToModel(content, options: TomlOptions);

            // Check if it has a [workspace] section but no [package] section
            var hasWorkspace = tomlTable.ContainsKey("workspace");
            var hasPackage = tomlTable.ContainsKey("package");

            return hasWorkspace && !hasPackage;
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Failed to check if {Path} is workspace-only", cargoTomlPath);
            return false;
        }
    }

    /// <summary>
    /// Processes a Cargo SBOM file asynchronously by parsing it and recording the lockfile version if available.
    /// </summary>
    /// <param name="processRequest">The process request containing the component stream for the SBOM file.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method delegates parsing to the <see cref="RustSbomDetector"/> and records the lockfile version
    /// in telemetry if the parsing operation returns a version number.
    /// </remarks>
    private async Task ProcessSbomFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken)
    {
        // Just before calling ParseAsync
        this.Logger.LogDebug(
            "SBOM parse starting. Recorder manifest location = {ManifestLocation}; SBOM stream location = {StreamLocation}",
            processRequest.SingleFileComponentRecorder.ManifestFileLocation,
            processRequest.ComponentStream.Location);

        int? version;
        if (this.ownershipMap != null)
        {
            version = await this.sbomParser.ParseWithOwnershipAsync(
                processRequest.ComponentStream,
                processRequest.SingleFileComponentRecorder,
                this.ComponentRecorder,
                this.ownershipMap,
                cancellationToken);
        }
        else
        {
            version = await this.sbomParser.ParseAsync(
                processRequest.ComponentStream,
                processRequest.SingleFileComponentRecorder,
                cancellationToken);
        }

        if (version.HasValue)
        {
            this.RecordLockfileVersion(version.Value);
        }
    }

    /// <summary>
    /// Processes a Cargo.toml file asynchronously by attempting to execute the Cargo CLI for metadata extraction.
    /// </summary>
    /// <param name="processRequest">The process request containing the component stream for the Cargo.toml file.</param>
    /// <param name="directory">The directory path where the Cargo.toml file is located.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method delegates parsing to the <see cref="RustCliDetector"/> which executes the 'cargo metadata' command.
    /// If the CLI parsing is successful, the method:
    /// <list type="bullet">
    /// <item><description>Adds all local package directories found in the workspace to the visited directories set to prevent duplicate processing.</description></item>
    /// <item><description>Marks the current directory as visited.</description></item>
    /// </list>
    /// If the CLI parsing fails, the directory is not marked as visited, allowing fallback to Cargo.lock parsing if available.
    /// </remarks>
    private async Task ProcessCargoTomlAsync(ProcessRequest processRequest, string directory, CancellationToken cancellationToken)
    {
        var normalized = this.pathUtilityService.NormalizePath(processRequest.ComponentStream.Location);
        if (this.manifestMetadataCache != null &&
            this.manifestMetadataCache.TryGetValue(normalized, out var cachedMetadata))
        {
            this.Logger.LogDebug("Using cached cargo metadata for {Location}", normalized);

            var parentRecorder = processRequest.SingleFileComponentRecorder.GetParentComponentRecorder();
            var result = await this.cliParser.ParseFromMetadataAsync(
                processRequest.ComponentStream,
                processRequest.SingleFileComponentRecorder,
                cachedMetadata,
                this.ComponentRecorder,
                this.ownershipMap,
                cancellationToken);

            if (result.Success)
            {
                foreach (var dir in result.LocalPackageDirectories)
                {
                    this.visitedDirs.Add(dir);
                }

                this.visitedDirs.Add(this.pathUtilityService.NormalizePath(directory));
            }
        }
        else
        {
            this.Logger.LogWarning("No cached cargo metadata for {Location}", processRequest.ComponentStream.Location);
        }
    }

    /// <summary>
    /// Processes a Cargo.lock file asynchronously by parsing it and extracting component information.
    /// </summary>
    /// <param name="processRequest">The process request containing the component stream for the Cargo.lock file.</param>
    /// <param name="directory">The directory path where the Cargo.lock file is located.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// <list type="number">
    /// <item><description>Delegates parsing of the Cargo.lock file to the <see cref="RustCrateDetector"/>.</description></item>
    /// <item><description>If parsing is successful and returns a lockfile version, records the version in telemetry.</description></item>
    /// <item><description>Checks if a corresponding Cargo.toml file exists in the same directory.</description></item>
    /// <item><description>If Cargo.toml exists, parses its workspace tables to extract member and exclude patterns.</description></item>
    /// <item><description>Marks the current directory as visited to prevent duplicate processing.</description></item>
    /// </list>
    /// The workspace table processing enables proper glob-based filtering of workspace members in subsequent detection operations.
    /// </remarks>
    private async Task ProcessCargoLockAsync(ProcessRequest processRequest, string directory, CancellationToken cancellationToken)
    {
        var version = await this.cargoLockParser.ParseAsync(
            processRequest.ComponentStream,
            processRequest.SingleFileComponentRecorder,
            cancellationToken);

        if (version.HasValue)
        {
            this.RecordLockfileVersion(version.Value);

            // Check if Cargo.toml exists in same directory to parse workspace tables
            var cargoTomlPath = Path.Combine(directory, "Cargo.toml");
            if (File.Exists(cargoTomlPath))
            {
                await this.ProcessWorkspaceTablesAsync(cargoTomlPath, directory);
            }

            // Add current directory to visitedDirs
            this.visitedDirs.Add(this.pathUtilityService.NormalizePath(directory));
        }
    }

    /// <summary>
    /// Processes the workspace tables from a Cargo.toml file asynchronously to extract member and exclude patterns.
    /// </summary>
    /// <param name="cargoTomlPath">The full path to the Cargo.toml file to parse.</param>
    /// <param name="directory">The directory path where the Cargo.toml file is located, used as the root for glob patterns.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method parses the [workspace] section of a Cargo.toml file to extract:
    /// <list type="bullet">
    /// <item><description><c>default-members</c> or <c>members</c> arrays as include patterns for workspace members.</description></item>
    /// <item><description><c>exclude</c> array as exclude patterns for workspace members.</description></item>
    /// </list>
    /// If include patterns are found, they are added as a glob rule with the specified directory as the root.
    /// This enables proper filtering of workspace members during subsequent detection operations.
    /// If parsing fails, a warning is logged and the method continues without throwing an exception.
    /// </remarks>
    private async Task ProcessWorkspaceTablesAsync(string cargoTomlPath, string directory)
    {
        try
        {
            var content = await File.ReadAllTextAsync(cargoTomlPath);
            var tomlTable = Toml.ToModel(content, options: TomlOptions);

            if (tomlTable.ContainsKey("workspace") && tomlTable["workspace"] is TomlTable workspaceTable)
            {
                var includes = new List<string>();
                var excludes = new List<string>();

                // Parse default-members or members
                if (workspaceTable.ContainsKey("default-members") && workspaceTable["default-members"] is TomlArray defaultMembers)
                {
                    includes.AddRange(defaultMembers.Cast<string>());
                }
                else if (workspaceTable.ContainsKey("members") && workspaceTable["members"] is TomlArray members)
                {
                    includes.AddRange(members.Cast<string>());
                }

                // Parse exclude
                if (workspaceTable.ContainsKey("exclude") && workspaceTable["exclude"] is TomlArray excludeArray)
                {
                    excludes.AddRange(excludeArray.Cast<string>());
                }

                if (includes.Count > 0)
                {
                    this.AddGlobRule(directory, includes, excludes);
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Failed to parse workspace tables from {Path}", cargoTomlPath);
        }
    }

    /// <summary>
    /// Represents a glob rule with root directory and include/exclude patterns.
    /// </summary>
    private class GlobRule
    {
        public string Root { get; set; }

        public List<string> Includes { get; set; }

        public List<string> Excludes { get; set; }

        public List<Glob> IncludeGlobs { get; set; }

        public List<Glob> ExcludeGlobs { get; set; }
    }
}

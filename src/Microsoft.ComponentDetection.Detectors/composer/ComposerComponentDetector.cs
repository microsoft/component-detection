namespace Microsoft.ComponentDetection.Detectors.Composer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>Reads Composer package files and optionally inventories the local PHP environment through Composer.</summary>
public sealed class ComposerComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private const string ComposerLockFileName = "composer.lock";
    private const string ComposerManifestFileName = "composer.json";
    private const string InstalledPackagesFileName = "installed.json";
    private const string ComposerName = "composer";
    private const string DefaultVendorDirectoryName = "vendor";
    private const string PackagesPropertyName = "packages";
    private const string DevPackageNamesPropertyName = "dev-package-names";
    private const string NamePropertyName = "name";
    private const string VersionPropertyName = "version";
    private const string RequirePropertyName = "require";
    private const string RequireDevPropertyName = "require-dev";
    private const string ProvidePropertyName = "provide";
    private const string ReplacePropertyName = "replace";
    private const string ExtraPropertyName = "extra";
    private const string BranchAliasPropertyName = "branch-alias";
    private const string AliasesPropertyName = "aliases";
    private const string SelfVersionConstraint = "self.version";
    private static readonly TimeSpan PlatformInventoryTimeout = TimeSpan.FromSeconds(10);

    private readonly IEnvironmentVariableService environmentVariableService;
    private readonly IFileUtilityService fileUtilityService;
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly ConcurrentHashSet<string> coveredVendorDirectories = [];
    private readonly ConcurrentHashSet<string> knownPlatformVendorDirectories = [];
    private readonly ConcurrentDictionary<ProcessRequest, PreparedProject> preparedProjects = [];

    public ComposerComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IEnvironmentVariableService environmentVariableService,
        IFileUtilityService fileUtilityService,
        ICommandLineInvocationService commandLineInvocationService,
        ILogger<ComposerComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.environmentVariableService = environmentVariableService;
        this.fileUtilityService = fileUtilityService;
        this.commandLineInvocationService = commandLineInvocationService;
        this.Logger = logger;
    }

    public override string Id => "Composer";

    public override IEnumerable<string> Categories => [nameof(DetectorClass.Composer)];

    public override IList<string> SearchPatterns => [ComposerLockFileName, ComposerManifestFileName];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Composer, ComponentType.PhpSdk];

    public override int Version => 1;

    public override bool NeedsAutomaticRootDependencyCalculation => false;

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        this.coveredVendorDirectories.Clear();
        this.knownPlatformVendorDirectories.Clear();
        this.preparedProjects.Clear();
        var files = await processRequests.ToList();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var group in files.GroupBy(request => Path.GetDirectoryName(request.ComponentStream.Location)!))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessRequest? lockfile = null;
            IComponentStream? manifest = null;
            foreach (var file in group)
            {
                var fileName = Path.GetFileName(file.ComponentStream.Location);
                if (fileName.Equals(ComposerLockFileName, StringComparison.OrdinalIgnoreCase))
                {
                    lockfile ??= file;
                }
                else if (fileName.Equals(ComposerManifestFileName, StringComparison.OrdinalIgnoreCase))
                {
                    manifest ??= file.ComponentStream;
                }

                if (lockfile is not null && manifest is not null)
                {
                    break;
                }
            }

            var request = lockfile ?? group.First();
            var project = await this.PrepareProjectAsync(request.ComponentStream.Location, manifest, cancellationToken);
            this.preparedProjects[request] = project;
            this.knownPlatformVendorDirectories.Add(project.VendorDirectory);
        }

        return this.OrderProjects(cancellationToken).ToObservable();
    }

    protected override Task OnDetectionFinishedAsync()
    {
        this.preparedProjects.Clear();
        return Task.CompletedTask;
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = processRequest.ComponentStream;

        // Check if current or a parent directory were scanned already to improve perf and avoid noise from child manifest files.
        for (var directory = Path.GetDirectoryName(file.Location); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (this.coveredVendorDirectories.Contains(directory))
            {
                this.Logger.LogDebug("Ignoring Composer file {Location} inside an already processed project's vendor directory.", file.Location);
                return;
            }
        }

        var recorder = processRequest.SingleFileComponentRecorder;
        this.Logger.LogDebug("Reading Composer file {Location}", file.Location);
        var isManifest = Path.GetFileName(file.Location).Equals(ComposerManifestFileName, StringComparison.OrdinalIgnoreCase);
        using var document = isManifest ? null : await this.ReadDocumentAsync(file.Stream, file.Location, recorder, cancellationToken);
        if (!isManifest && document is null)
        {
            return;
        }

        var project = this.preparedProjects[processRequest];
        var manifestRoot = project.Manifest;
        if (project.ManifestError is not null)
        {
            this.Logger.LogError(project.ManifestError, "Invalid Composer JSON in {Location}", project.ManifestLocation);
            recorder.RegisterPackageParseFailure(project.ManifestLocation);
        }
        else if (manifestRoot is not null && manifestRoot.Value.ValueKind != JsonValueKind.Object)
        {
            this.ReportFailure(recorder, project.ManifestLocation, "Expected a JSON object.");
            manifestRoot = null;
        }

        if (isManifest && manifestRoot is null)
        {
            return;
        }

        var vendorDirectory = project.VendorDirectory;
        await this.RegisterPlatformInventoryAsync(file.Location, recorder, cancellationToken);

        var packages = new Dictionary<string, LockedPackage>();
        if (isManifest)
        {
            if (!await this.ReadInstalledPackagesAsync(vendorDirectory, packages, recorder, cancellationToken))
            {
                this.Logger.LogWarning(
                    $"Composer manifest {{Location}} without a sibling {ComposerLockFileName} or usable {InstalledPackagesFileName}. Provide either to improve accuracy; otherwise only exact versions are detected.",
                    file.Location);
                this.ReadManifestRequirements(manifestRoot!.Value, recorder, file.Location, cancellationToken);
                return;
            }
        }
        else
        {
            this.ReadPackages(document!.RootElement, PackagesPropertyName, false, packages, recorder, cancellationToken);
            this.ReadPackages(document.RootElement, "packages-dev", true, packages, recorder, cancellationToken);
            this.ReadLockAliases(document.RootElement, packages, recorder, cancellationToken);
        }

        this.coveredVendorDirectories.Add(vendorDirectory);
        this.RegisterPackages(packages, manifestRoot, recorder, cancellationToken);
    }

    private static Dictionary<string, List<(LockedPackage Package, string Constraint)>> CreateProviderLookup(Dictionary<string, LockedPackage> packages)
    {
        var providers = new Dictionary<string, List<(LockedPackage Package, string Constraint)>>(StringComparer.Ordinal);
        foreach (var package in packages.Values)
        {
            foreach (var (name, constraint) in package.Provides.Concat(package.Replaces))
            {
                if (!providers.TryGetValue(name, out var claims))
                {
                    providers[name] = claims = [];
                }

                if (constraint == SelfVersionConstraint)
                {
                    claims.Add((package, package.Component.Version));
                    foreach (var alias in package.Aliases)
                    {
                        claims.Add((package, alias));
                    }
                }
                else
                {
                    claims.Add((package, constraint));
                }
            }
        }

        return providers;
    }

    private static bool TryGetNonEmptyString(JsonElement entry, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
        return !string.IsNullOrWhiteSpace(value);
    }

    private void RegisterPackages(
        Dictionary<string, LockedPackage> packages,
        JsonElement? manifestRoot,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken)
    {
        // Names and aliases are lookup keys, never additional detected components.
        var names = packages.Values.GroupBy(package => package.Component.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var providers = CreateProviderLookup(packages);
        var rootClaims = manifestRoot is null
            ? []
            : this.ReadRootClaims(manifestRoot.Value, recorder);
        var roots = manifestRoot is null
            ? []
            : this.ReadLinks(manifestRoot.Value, RequirePropertyName, recorder, ComposerManifestFileName)
                .Concat(this.ReadLinks(manifestRoot.Value, RequireDevPropertyName, recorder, ComposerManifestFileName))
                .Select(pair => this.Resolve(pair.Key, pair.Value, names, providers, rootClaims)?.Component.Id)
                .OfType<string>().ToHashSet();

        foreach (var package in packages.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            recorder.RegisterUsage(new DetectedComponent(package.Component), isExplicitReferencedDependency: roots.Contains(package.Component.Id), isDevelopmentDependency: package.IsDevelopment);
        }

        foreach (var package in packages.Values)
        {
            foreach (var (name, constraint) in package.Requires)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dependency = this.Resolve(name, constraint, names, providers, rootClaims);
                if (dependency is not null && dependency.Component.Id != package.Component.Id)
                {
                    recorder.RegisterUsage(new DetectedComponent(dependency.Component), parentComponentId: package.Component.Id, isDevelopmentDependency: dependency.IsDevelopment);
                }
            }
        }

        if (manifestRoot is null)
        {
            foreach (var root in ComposerGraph.FindRoots(recorder.DependencyGraph, packages.Keys, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var package = packages[root];
                recorder.RegisterUsage(new DetectedComponent(package.Component), isExplicitReferencedDependency: true, isDevelopmentDependency: package.IsDevelopment);
            }
        }
    }

    private async Task RegisterPlatformInventoryAsync(string location, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        for (var directory = Path.GetDirectoryName(location); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (this.knownPlatformVendorDirectories.Contains(directory) || Path.GetFileName(directory).Equals(DefaultVendorDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                this.Logger.LogDebug("Skipping Composer platform inventory for {Location} inside a vendor directory.", location);
                return;
            }
        }

        if (!this.commandLineInvocationService.IsCommandLineExecution())
        {
            this.Logger.LogDebug("Composer platform inventory requires command-line execution; only package files will be scanned.");
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PlatformInventoryTimeout);
        CommandLineExecutionResult result;
        try
        {
            var projectDirectory = Path.GetDirectoryName(location)!;
            if (projectDirectory.IndexOfAny(['"', '\r', '\n']) >= 0 ||
                (OperatingSystem.IsWindows() && projectDirectory.IndexOfAny(['%', '!']) >= 0))
            {
                this.Logger.LogWarning("Composer platform inventory cannot safely pass project directory {Directory} to the command line; continuing with package files only.", projectDirectory);
                return;
            }

            // Avoid a trailing backslash escaping the argument's closing quote for filesystem roots.
            projectDirectory = Path.EndsInDirectorySeparator(projectDirectory) ? $"{projectDirectory}." : projectDirectory;
            string[] arguments = ["--no-plugins", "--no-scripts", "--no-interaction", "--no-ansi", $"--working-dir=\"{projectDirectory}\"", "show", "--platform", "--format=json"];
            result = await this.commandLineInvocationService.ExecuteCommandAsync(
                ComposerName, [$"{ComposerName}.cmd", $"{ComposerName}.bat"], workingDirectory: null, cancellationToken: timeout.Token, parameters: arguments);
            timeout.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            this.Logger.LogWarning("Composer platform inventory exceeded its {TimeoutSeconds}-second limit; continuing with package files only.", PlatformInventoryTimeout.TotalSeconds);
            return;
        }
        catch (Exception exception)
        {
            this.Logger.LogWarning(exception, "Unable to run Composer platform inventory; continuing with package files only.");
            return;
        }

        if (result.ExitCode != 0)
        {
            this.Logger.LogWarning("Composer platform inventory failed with exit code {ExitCode}: {StdErr}", result.ExitCode, result.StdErr);
            return;
        }

        this.RegisterPlatformComponents(result.StdOut, recorder, cancellationToken);
    }

    private void RegisterPlatformComponents(string output, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("platform", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                this.Logger.LogWarning("Composer platform inventory did not contain a platform array; no environment components will be reported.");
                return;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetNonEmptyString(entry, NamePropertyName, out var packageName) ||
                    !TryGetNonEmptyString(entry, VersionPropertyName, out var version))
                {
                    this.Logger.LogWarning("Composer platform inventory contained an invalid package identity; the entry is omitted.");
                    continue;
                }

                if (packageName.Contains('/', StringComparison.Ordinal) ||
                    (packageName is not ("php" or "hhvm" or ComposerName) &&
                    !packageName.StartsWith("ext-", StringComparison.Ordinal) && !packageName.StartsWith("lib-", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (entry.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String &&
                    description.GetString()!.Contains("via config.platform", StringComparison.Ordinal))
                {
                    this.Logger.LogDebug("Composer platform entry {Name} is simulated or disabled by config.platform; its configured version is not reported as installed.", packageName);
                    continue;
                }

                var hasDirectDependency = entry.TryGetProperty("direct-dependency", out var directDependency);
                if (hasDirectDependency && directDependency.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    this.Logger.LogWarning("Composer platform entry {Name} has invalid direct-dependency metadata; it is not marked explicitly referenced.", packageName);
                }

                recorder.RegisterUsage(
                    new DetectedComponent(new PhpSdkComponent(packageName, version)),
                    isExplicitReferencedDependency: hasDirectDependency && directDependency.ValueKind == JsonValueKind.True,
                    isDevelopmentDependency: true);
            }
        }
        catch (JsonException exception)
        {
            this.Logger.LogWarning(exception, "Composer platform inventory was not valid JSON; no environment components will be reported.");
        }
    }

    private void MergeLinks(Dictionary<string, string> target, Dictionary<string, string> source, HashSet<string> conflicts, string section, ISingleFileComponentRecorder recorder, string location)
    {
        foreach (var (name, constraint) in source)
        {
            var key = $"{section}.{name}";
            if (conflicts.Contains(key))
            {
                continue;
            }

            if (target.TryGetValue(name, out var existing) && existing != constraint)
            {
                this.ReportFailure(recorder, $"{location}.{key}", "Duplicate locked identity declares conflicting constraints; this dependency declaration is omitted.");
                target.Remove(name);
                conflicts.Add(key);
            }
            else
            {
                target.TryAdd(name, constraint);
            }
        }
    }

    private string GetVendorDirectory(string location, JsonElement? manifest)
    {
        var projectDirectory = Path.GetDirectoryName(location)!;
        var defaultVendorDirectory = Path.Combine(projectDirectory, DefaultVendorDirectoryName);
        var vendorDirectory = this.environmentVariableService.GetEnvironmentVariable("COMPOSER_VENDOR_DIR");
        if (string.IsNullOrWhiteSpace(vendorDirectory))
        {
            vendorDirectory = DefaultVendorDirectoryName;
            if (manifest is not null && manifest.Value.TryGetProperty("config", out var config))
            {
                if (config.ValueKind != JsonValueKind.Object)
                {
                    this.Logger.LogWarning("Invalid Composer config at {Location}; using the default vendor directory.", location);
                    return defaultVendorDirectory;
                }

                if (config.TryGetProperty("vendor-dir", out var configuredDirectory))
                {
                    if (configuredDirectory.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(configuredDirectory.GetString()))
                    {
                        this.Logger.LogWarning("Invalid Composer config.vendor-dir at {Location}; using the default vendor directory.", location);
                        return defaultVendorDirectory;
                    }

                    vendorDirectory = configuredDirectory.GetString()!;
                }
            }
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(vendorDirectory, projectDirectory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            this.Logger.LogWarning(exception, "Invalid Composer vendor directory at {Location}; using the default vendor directory.", location);
            return defaultVendorDirectory;
        }
    }

    private async Task<bool> ReadInstalledPackagesAsync(string vendorDirectory, Dictionary<string, LockedPackage> packages, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        var location = Path.Combine(vendorDirectory, ComposerName, InstalledPackagesFileName);
        if (!this.fileUtilityService.Exists(location))
        {
            return false;
        }

        JsonDocument? inventory;
        try
        {
            using var stream = this.fileUtilityService.MakeFileStream(location);
            inventory = await this.ReadDocumentAsync(stream, location, recorder, cancellationToken, allowPackageArray: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogWarning(exception, "Unable to read Composer installed inventory at {Location}; falling back to manifest requirements.", location);
            recorder.RegisterPackageParseFailure(location);
            return false;
        }

        using var document = inventory;
        if (document is null)
        {
            return false;
        }

        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && (!root.TryGetProperty(PackagesPropertyName, out var entries) || entries.ValueKind != JsonValueKind.Array))
        {
            this.ReportFailure(recorder, location, $"Expected an installed package array in {PackagesPropertyName}.");
            return false;
        }

        this.ReadPackages(root, PackagesPropertyName, false, packages, recorder, cancellationToken);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(DevPackageNamesPropertyName, out var devNames))
        {
            return true;
        }

        if (devNames.ValueKind != JsonValueKind.Array)
        {
            this.ReportFailure(recorder, $"{location}.{DevPackageNamesPropertyName}", "Expected an array of development package names.");
            return true;
        }

        var developmentPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in devNames.EnumerateArray())
        {
            if (name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()))
            {
                this.ReportFailure(recorder, $"{location}.{DevPackageNamesPropertyName}", "Expected a nonempty development package name.");
                continue;
            }

            developmentPackages.Add(name.GetString()!.Trim());
        }

        foreach (var package in packages.Values)
        {
            package.IsDevelopment = developmentPackages.Contains(package.Component.Name.Trim());
        }

        return true;
    }

    private async Task<JsonDocument?> ReadDocumentAsync(Stream stream, string location, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken, bool allowPackageArray = false)
    {
        try
        {
            var document = await JsonDocument.ParseAsync(stream, LenientJsonDocumentOptions, cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object || (allowPackageArray && document.RootElement.ValueKind == JsonValueKind.Array))
            {
                return document;
            }

            document.Dispose();
            this.ReportFailure(recorder, location, allowPackageArray ? "Expected a JSON object or installed package array." : "Expected a JSON object.");
        }
        catch (JsonException exception)
        {
            this.Logger.LogError(exception, "Invalid Composer JSON in {Location}", location);
            recorder.RegisterPackageParseFailure(location);
        }

        return null;
    }

    private async Task<PreparedProject> PrepareProjectAsync(string location, IComponentStream? manifest, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(location)!);

        // The factory owns its streams, so keep the enumerator alive until the manifest has been parsed and cloned.
        using var manifests = manifest is null
            ? this.ComponentStreamEnumerableFactory.GetComponentStreams(directory, [ComposerManifestFileName], null, recursivelyScanDirectories: false).GetEnumerator()
            : null;
        if (manifests?.MoveNext() == true)
        {
            manifest = manifests.Current;
        }

        JsonElement? root = null;
        JsonException? error = null;
        if (manifest is not null)
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(manifest.Stream, LenientJsonDocumentOptions, cancellationToken);
                root = document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                // Report invalid manifests only if their project is processed, not when covered by an owner's inventory.
                error = exception;
            }
        }

        var vendorDirectory = this.GetVendorDirectory(location, root is { ValueKind: JsonValueKind.Object } ? root : null);
        return new PreparedProject(vendorDirectory, manifest?.Location ?? Path.Combine(directory.FullName, ComposerManifestFileName), root, error);
    }

    private List<ProcessRequest> OrderProjects(CancellationToken cancellationToken)
    {
        var projectDirectories = this.preparedProjects.Keys.ToDictionary(request => request, request => Path.GetDirectoryName(request.ComponentStream.Location)!);
        var ownersByVendorDirectory = this.GetVendorOwnersByDirectory(projectDirectories);
        var dependents = projectDirectories.Keys.ToDictionary(request => request, _ => new List<ProcessRequest>());
        var remainingOwners = new Dictionary<ProcessRequest, int>(projectDirectories.Count);
        foreach (var (request, projectDirectory) in projectDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ownerCount = 0;
            for (var directory = projectDirectory; directory is not null; directory = Path.GetDirectoryName(directory))
            {
                if (ownersByVendorDirectory.TryGetValue(directory, out var matches))
                {
                    // Each owner has one vendor directory, so it can only match once along this path.
                    foreach (var owner in matches)
                    {
                        if (owner != request)
                        {
                            dependents[owner].Add(request);
                            ownerCount++;
                        }
                    }
                }
            }

            remainingOwners.Add(request, ownerCount);
        }

        var ready = new Queue<ProcessRequest>(remainingOwners.Where(entry => entry.Value == 0).Select(entry => entry.Key));
        var ordered = new List<ProcessRequest>(projectDirectories.Count);
        while (ready.TryDequeue(out var request))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ordered.Add(request);
            foreach (var dependent in dependents[request])
            {
                if (--remainingOwners[dependent] == 0)
                {
                    ready.Enqueue(dependent);
                }
            }
        }

        if (ordered.Count != projectDirectories.Count)
        {
            this.Logger.LogWarning("Composer vendor directory ownership contains a cycle; some projects cannot be processed before their vendor inputs.");
            ordered.AddRange(remainingOwners.Where(entry => entry.Value > 0).Select(entry => entry.Key));
        }

        return ordered;
    }

    private Dictionary<string, List<ProcessRequest>> GetVendorOwnersByDirectory(Dictionary<ProcessRequest, string> projectDirectories)
    {
        var ownersByVendorDirectory = new Dictionary<string, List<ProcessRequest>>(StringComparer.Ordinal);
        foreach (var group in this.preparedProjects.GroupBy(project => project.Value.VendorDirectory))
        {
            var vendorPrefix = Path.EndsInDirectorySeparator(group.Key) ? group.Key : $"{group.Key}{Path.DirectorySeparatorChar}";

            // An absolute COMPOSER_VENDOR_DIR also applies to package manifests inside that directory; they are not additional owners.
            var externalOwners = group.Where(project => !projectDirectories[project.Key].StartsWith(vendorPrefix, StringComparison.Ordinal))
                .Select(project => project.Key).ToList();
            ownersByVendorDirectory.Add(group.Key, externalOwners.Count > 0 ? externalOwners : group.Select(project => project.Key).ToList());
        }

        return ownersByVendorDirectory;
    }

    private void ReadPackages(JsonElement root, string section, bool isDevelopment, Dictionary<string, LockedPackage> packages, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        var entries = root;
        if (root.ValueKind != JsonValueKind.Array && !root.TryGetProperty(section, out entries))
        {
            return;
        }

        if (entries.ValueKind != JsonValueKind.Array)
        {
            this.ReportFailure(recorder, section, "Expected a package array.");
            return;
        }

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = $"{section}[{index++}]";
            if (!TryGetNonEmptyString(entry, NamePropertyName, out var name) ||
                !TryGetNonEmptyString(entry, VersionPropertyName, out var version))
            {
                this.ReportFailure(recorder, location, "Expected a package with nonempty string name and version.");
                continue;
            }

            var packageName = name.Trim();
            var separator = packageName.IndexOf('/');
            if (separator <= 0 || separator == packageName.Length - 1)
            {
                this.ReportFailure(recorder, location, "Expected a nonempty vendor and package name for a Composer package URL.");
                continue;
            }

            var component = new ComposerComponent(name, version);
            var package = new LockedPackage(
                component,
                isDevelopment,
                this.ReadLinks(entry, RequirePropertyName, recorder, location),
                this.ReadLinks(entry, ProvidePropertyName, recorder, location),
                this.ReadLinks(entry, ReplacePropertyName, recorder, location),
                this.ReadBranchAliases(entry, component.Version.Trim(), recorder, location));
            if (packages.TryGetValue(component.Id, out var existing))
            {
                // A package needed in production is never development-only, irrespective of entry order.
                existing.IsDevelopment &= isDevelopment;
                this.MergeLinks(existing.Requires, package.Requires, existing.Conflicts, RequirePropertyName, recorder, location);
                this.MergeLinks(existing.Provides, package.Provides, existing.Conflicts, ProvidePropertyName, recorder, location);
                this.MergeLinks(existing.Replaces, package.Replaces, existing.Conflicts, ReplacePropertyName, recorder, location);
                existing.Aliases.UnionWith(package.Aliases);
            }
            else
            {
                packages.Add(component.Id, package);
            }
        }
    }

    private HashSet<string> ReadBranchAliases(JsonElement entry, string version, ISingleFileComponentRecorder recorder, string location)
    {
        if (!entry.TryGetProperty(ExtraPropertyName, out var extra) || extra.ValueKind != JsonValueKind.Object ||
            !extra.TryGetProperty(BranchAliasPropertyName, out var aliases))
        {
            return [];
        }

        var aliasLocation = $"{location}.{ExtraPropertyName}.{BranchAliasPropertyName}";
        if (aliases.ValueKind != JsonValueKind.Object)
        {
            this.ReportFailure(recorder, aliasLocation, "Expected a branch alias object.");
            return [];
        }

        if (aliases.TryGetProperty(version, out var alias))
        {
            var aliasValue = alias.ValueKind == JsonValueKind.String ? alias.GetString() : null;
            if (!string.IsNullOrWhiteSpace(aliasValue))
            {
                return [aliasValue.Trim()];
            }

            this.ReportFailure(recorder, $"{aliasLocation}.{version}", "Expected a nonempty branch alias.");
        }

        return [];
    }

    private void ReadLockAliases(JsonElement root, Dictionary<string, LockedPackage> packages, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty(AliasesPropertyName, out var aliases))
        {
            return;
        }

        if (aliases.ValueKind != JsonValueKind.Array)
        {
            this.ReportFailure(recorder, AliasesPropertyName, "Expected an alias array.");
            return;
        }

        var packagesByName = packages.Values.ToLookup(package => package.Component.Name.Trim(), StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in aliases.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = $"{AliasesPropertyName}[{index++}]";
            if (!TryGetNonEmptyString(entry, "package", out var name) ||
                !TryGetNonEmptyString(entry, VersionPropertyName, out var version) ||
                !TryGetNonEmptyString(entry, "alias", out var alias))
            {
                this.ReportFailure(recorder, location, "Expected nonempty string package, version and alias fields.");
                continue;
            }

            var matching = packagesByName[name.Trim()]
                .Where(package => ComposerVersionConstraint.VersionsEqual(package.Component.Version, version)).ToList();
            if (matching.Count == 1)
            {
                matching[0].Aliases.Add(alias.Trim());
            }
            else
            {
                this.Logger.LogWarning("Composer alias at {Location} does not uniquely identify an installed package.", location);
            }
        }
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Composer package lookup names require lowercase.")]
    private Dictionary<string, string> ReadLinks(JsonElement entry, string section, ISingleFileComponentRecorder recorder, string location)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        if (!entry.TryGetProperty(section, out var value))
        {
            return links;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            this.ReportFailure(recorder, $"{location}.{section}", "Expected a dependency object.");
            return links;
        }

        foreach (var property in value.EnumerateObject())
        {
            var name = property.Name.Trim().ToLowerInvariant();
            if (property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                this.ReportFailure(recorder, $"{location}.{section}.{name}", "Expected a nonempty version constraint.");
                continue;
            }

            // php, ext-*, lib-* and composer-* platform requirements do not have a vendor namespace.
            // Names such as composer/installers and ext-vendor/package are real packages, not platforms.
            if (!name.Contains('/', StringComparison.Ordinal))
            {
                this.Logger.LogDebug(
                    "Skipping Composer dependency {Name} at {Location} without a vendor namespace; it may be a platform requirement.",
                    name,
                    $"{location}.{section}");
                continue;
            }

            if (name.StartsWith('/') || name.EndsWith('/'))
            {
                this.ReportFailure(recorder, $"{location}.{section}.{name}", "Expected a nonempty vendor and package name for a Composer package URL.");
                continue;
            }

            var constraint = property.Value.GetString()!.Trim();
            if (conflicts.Contains(name))
            {
                continue;
            }

            if (links.TryGetValue(name, out var previous) && previous != constraint)
            {
                this.ReportFailure(recorder, $"{location}.{section}.{name}", "Duplicate dependency name declares conflicting constraints; the declaration is omitted.");
                links.Remove(name);
                conflicts.Add(name);
            }
            else
            {
                links[name] = constraint;
            }
        }

        return links;
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Composer package lookup names require lowercase.")]
    private void ReadManifestRequirements(JsonElement root, ISingleFileComponentRecorder recorder, string location, CancellationToken cancellationToken)
    {
        foreach (var (section, isDevelopment) in new[] { (RequirePropertyName, false), (RequireDevPropertyName, true) })
        {
            var requirements = this.ReadLinks(root, section, recorder, location);
            if (requirements.Count == 0)
            {
                continue;
            }

            foreach (var property in root.GetProperty(section).EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = property.Name;
                if (!requirements.Remove(name.Trim().ToLowerInvariant(), out var constraint))
                {
                    continue;
                }

                if (!ComposerVersionConstraint.TryGetExactVersion(constraint, out var version))
                {
                    this.Logger.LogWarning(
                        $"Composer dependency {{Name}} at {{Location}} has unresolved constraint {{Constraint}}; this declaration is omitted. Provide {ComposerLockFileName}, {InstalledPackagesFileName}, or an exact version to improve accuracy.",
                        name,
                        location,
                        constraint);
                    continue;
                }

                recorder.RegisterUsage(
                    new DetectedComponent(new ComposerComponent(name, version)),
                    isExplicitReferencedDependency: true,
                    isDevelopmentDependency: isDevelopment);
            }
        }
    }

    private Dictionary<string, List<string>> ReadRootClaims(JsonElement root, ISingleFileComponentRecorder recorder)
    {
        var claims = this.ReadLinks(root, ProvidePropertyName, recorder, ComposerManifestFileName)
            .Concat(this.ReadLinks(root, ReplacePropertyName, recorder, ComposerManifestFileName))
            .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Select(pair => pair.Value).ToList());
        if (claims.Values.Any(constraints => constraints.Contains(SelfVersionConstraint)) &&
            TryGetNonEmptyString(root, VersionPropertyName, out var version))
        {
            var rootVersion = version.Trim();
            var versions = this.ReadBranchAliases(root, rootVersion, recorder, ComposerManifestFileName);
            versions.Add(rootVersion);
            foreach (var name in claims.Keys.ToList())
            {
                claims[name] = claims[name].SelectMany(constraint => constraint == SelfVersionConstraint ? versions : [constraint]).ToList();
            }
        }

        // Without a declared root version, self.version cannot be inferred offline from Git.
        // Leave it unresolved so Resolve declines to guess an edge to an installed provider.
        return claims;
    }

    private LockedPackage? Resolve(
        string name,
        string constraint,
        Dictionary<string, List<LockedPackage>> names,
        Dictionary<string, List<(LockedPackage Package, string Constraint)>> providers,
        Dictionary<string, List<string>> rootClaims)
    {
        if (names.TryGetValue(name, out var actual))
        {
            if (actual.Count == 1)
            {
                // Resolved identities are authoritative, including branch aliases and references.
                return actual[0];
            }

            this.Logger.LogWarning("Composer dependency {Name} has multiple resolved versions; no edge can be inferred.", name);
            return null;
        }

        if (rootClaims.TryGetValue(name, out var rootConstraints))
        {
            foreach (var rootConstraint in rootConstraints)
            {
                if (!ComposerVersionConstraint.TryIntersects(constraint, rootConstraint, out var compatible))
                {
                    this.Logger.LogWarning("Composer root virtual dependency {Name} has an unresolved constraint; no edge can be inferred offline.", name);
                    return null;
                }

                if (compatible)
                {
                    // Root-provided virtual dependencies are not components of the locked package graph.
                    return null;
                }
            }
        }

        if (!providers.TryGetValue(name, out var claims))
        {
            this.Logger.LogWarning("Composer dependency {Name} is not present in the resolved package inventory.", name);
            return null;
        }

        var candidates = new HashSet<LockedPackage>();
        foreach (var (package, declaredConstraint) in claims)
        {
            if (!ComposerVersionConstraint.TryIntersects(constraint, declaredConstraint, out var compatible))
            {
                this.Logger.LogWarning("Composer virtual dependency {Name} has an unsupported constraint; provider uniqueness cannot be established.", name);
                return null;
            }

            if (compatible)
            {
                candidates.Add(package);
            }
        }

        if (candidates.Count == 1)
        {
            return candidates.Single();
        }

        this.Logger.LogWarning("Composer virtual dependency {Name} has {Count} compatible installed providers; no edge can be inferred.", name, candidates.Count);
        return null;
    }

    private void ReportFailure(ISingleFileComponentRecorder recorder, string location, string reason)
    {
        this.Logger.LogError("Malformed Composer data at {Location}: {Reason}", location, reason);
        recorder.RegisterPackageParseFailure(location);
    }

    private sealed record PreparedProject(string VendorDirectory, string ManifestLocation, JsonElement? Manifest, JsonException? ManifestError);

    private sealed class LockedPackage(
        ComposerComponent component,
        bool isDevelopment,
        Dictionary<string, string> requires,
        Dictionary<string, string> provides,
        Dictionary<string, string> replaces,
        HashSet<string> aliases)
    {
        internal ComposerComponent Component { get; } = component;

        internal bool IsDevelopment { get; set; } = isDevelopment;

        internal Dictionary<string, string> Requires { get; } = requires;

        internal Dictionary<string, string> Provides { get; } = provides;

        internal Dictionary<string, string> Replaces { get; } = replaces;

        internal HashSet<string> Aliases { get; } = aliases;

        internal HashSet<string> Conflicts { get; } = [];
    }
}

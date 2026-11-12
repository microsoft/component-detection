namespace Microsoft.ComponentDetection.Detectors.Composer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Composer.Contracts;
using Microsoft.ComponentDetection.Detectors.Composer.Parsers;
using Microsoft.Extensions.Logging;

/// <summary>Reads Composer package files and optionally inventories the local PHP environment through Composer.</summary>
public sealed class ComposerComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private const string ComposerLockFileName = "composer.lock";
    private const string ComposerManifestFileName = "composer.json";
    private const string InstalledPackagesFileName = "installed.json";
    private const string ComposerName = "composer";
    private const string DefaultVendorDirectoryName = "vendor";
    private static readonly TimeSpan PlatformInventoryTimeout = TimeSpan.FromSeconds(10);

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly ComposerManifestParser manifestParser;
    private readonly ComposerPackageParser packageParser;
    private readonly ComposerPlatformParser platformParser;
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
        this.commandLineInvocationService = commandLineInvocationService;
        this.Logger = logger;
        this.manifestParser = new ComposerManifestParser(environmentVariableService, LenientJsonSerializerOptions, logger);
        this.packageParser = new ComposerPackageParser(fileUtilityService, this.manifestParser, LenientJsonSerializerOptions, logger);
        this.platformParser = new ComposerPlatformParser(LenientJsonSerializerOptions, logger);
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
        var lockFile = isManifest ? null : await this.packageParser.ReadLockFileAsync(file.Stream, file.Location, recorder, cancellationToken);
        if (!isManifest && lockFile is null)
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

        if (isManifest && manifestRoot is null)
        {
            return;
        }

        var vendorDirectory = project.VendorDirectory;
        await this.RegisterPlatformInventoryAsync(file.Location, recorder, cancellationToken);

        var packages = isManifest
            ? await this.packageParser.ReadInstalledPackagesAsync(vendorDirectory, recorder, cancellationToken)
            : this.packageParser.ReadLockFile(lockFile!, recorder, cancellationToken);
        if (packages is null)
        {
            this.Logger.LogWarning(
                $"Composer manifest {{Location}} without a sibling {ComposerLockFileName} or usable {InstalledPackagesFileName}. Provide either to improve accuracy; otherwise only exact versions are detected.",
                file.Location);
            this.manifestParser.RegisterRequirements(manifestRoot!, recorder, file.Location, cancellationToken);
            return;
        }

        this.coveredVendorDirectories.Add(vendorDirectory);
        this.packageParser.RegisterPackages(packages, manifestRoot, recorder, cancellationToken);
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

        this.platformParser.RegisterComponents(result.StdOut, recorder, cancellationToken);
    }

    private async Task<PreparedProject> PrepareProjectAsync(string location, IComponentStream? manifest, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(location)!);

        // The factory owns its streams, so keep the enumerator alive until deserialization finishes.
        using var manifests = manifest is null
            ? this.ComponentStreamEnumerableFactory.GetComponentStreams(directory, [ComposerManifestFileName], null, recursivelyScanDirectories: false).GetEnumerator()
            : null;
        if (manifests?.MoveNext() == true)
        {
            manifest = manifests.Current;
        }

        ComposerManifest? root = null;
        JsonException? error = null;
        if (manifest is not null)
        {
            try
            {
                root = await this.manifestParser.ReadAsync(manifest.Stream, cancellationToken);
            }
            catch (JsonException exception)
            {
                // Report invalid manifests only if their project is processed, not when covered by an owner's inventory.
                error = exception;
            }
        }

        var vendorDirectory = this.manifestParser.GetVendorDirectory(location, root);
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

    private sealed record PreparedProject(string VendorDirectory, string ManifestLocation, ComposerManifest? Manifest, JsonException? ManifestError);
}

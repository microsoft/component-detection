namespace Microsoft.ComponentDetection.Detectors.Npm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.Extensions.Logging;

public abstract class NpmLockfileDetectorBase : FileComponentDetector
{
    private const string NpmRegistryHost = "registry.npmjs.org";

    private const string LernaSearchPattern = "lerna.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonDocumentOptions JsonDocumentOptions = new()
    {
        AllowTrailingCommas = true,
    };

    private readonly object lernaFilesLock = new object();

    /// <summary>
    /// Gets or sets the logger for writing basic logging message to both console and file.
    /// </summary>
    private readonly IPathUtilityService pathUtilityService;

    protected NpmLockfileDetectorBase(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPathUtilityService pathUtilityService,
        ILogger logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.pathUtilityService = pathUtilityService;
        this.Logger = logger;
    }

    protected NpmLockfileDetectorBase(IPathUtilityService pathUtilityService) => this.pathUtilityService = pathUtilityService;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Npm)!];

    public override IList<string> SearchPatterns { get; } = ["package-lock.json", "npm-shrinkwrap.json", LernaSearchPattern];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Npm];

    private List<ProcessRequest> LernaFiles { get; } = [];

    /// <inheritdoc />
    protected override IList<string> SkippedFolders => ["node_modules", "pnpm-store"];

    protected abstract bool IsSupportedLockfileVersion(int lockfileVersion);

    protected abstract void ProcessLockfile(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        PackageJson packageJson,
        JsonDocument lockfile,
        int lockfileVersion);

    protected override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(this.RemoveNodeModuleNestedFiles(processRequests)
            .Where(pr =>
            {
                if (!pr.ComponentStream.Pattern.Equals(LernaSearchPattern))
                {
                    return true;
                }

                // Lock LernaFiles so we don't add while we are enumerating in processFiles
                lock (this.lernaFilesLock)
                {
                    this.LernaFiles.Add(pr);
                    return false;
                }
            }));

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> packageJsonPattern = ["package.json"];
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var packageJsonComponentStream = this.ComponentStreamEnumerableFactory.GetComponentStreams(new FileInfo(file.Location).Directory, packageJsonPattern, (name, directoryName) => false, false);

        IList<ProcessRequest> lernaFilesClone;

        // ToList LernaFiles to generate a copy we can act on without holding the lock for a long time
        lock (this.lernaFilesLock)
        {
            lernaFilesClone = this.LernaFiles.ToList();
        }

        var foundUnderLerna = lernaFilesClone.Select(lernaProcessRequest => lernaProcessRequest.ComponentStream)
            .Any(lernaFile => this.pathUtilityService.IsFileBelowAnother(
                lernaFile.Location,
                file.Location));

        try
        {
            using var lockfileDocument = await JsonDocument.ParseAsync(file.Stream, JsonDocumentOptions, cancellationToken);
            var root = lockfileDocument.RootElement;

            // Validate name and version unless under lerna
            if (!foundUnderLerna)
            {
                var hasName = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nameElement.GetString());
                var hasVersion = root.TryGetProperty("version", out var versionElement) && versionElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(versionElement.GetString());

                if (!hasName || !hasVersion)
                {
                    this.Logger.LogInformation("{PackageLogJsonFile} does not contain a valid name and/or version. These are required fields for a valid package-lock.json file. It and its dependencies will not be registered.", file.Location);
                    return;
                }
            }

            var lockfileVersion = root.TryGetProperty("lockfileVersion", out var lockfileVersionElement) ? lockfileVersionElement.GetInt32() : 1;
            this.RecordLockfileVersion(lockfileVersion);

            if (!this.IsSupportedLockfileVersion(lockfileVersion))
            {
                return;
            }

            // Read package.json files
            var packageJsons = new List<PackageJson>();
            foreach (var stream in packageJsonComponentStream)
            {
                try
                {
                    var packageJson = await JsonSerializer.DeserializeAsync<PackageJson>(stream.Stream, JsonOptions, cancellationToken);
                    if (packageJson is not null)
                    {
                        packageJsons.Add(packageJson);
                    }
                }
                catch (JsonException ex)
                {
                    this.Logger.LogWarning(ex, "Could not parse package.json at {Location}", stream.Location);
                }
            }

            if (packageJsons.Count == 0)
            {
                throw new InvalidOperationException(string.Format("InvalidPackageJson -- There must be a package.json file at '{0}' for components to be registered", singleFileComponentRecorder.ManifestFileLocation));
            }

            // Process each package.json against the lockfile
            foreach (var packageJson in packageJsons)
            {
                this.ProcessLockfile(singleFileComponentRecorder, packageJson, lockfileDocument, lockfileVersion);
            }
        }
        catch (JsonException ex)
        {
            this.Logger.LogInformation(ex, "Could not parse JSON from {ComponentLocation} file.", file.Location);
        }
        catch (InvalidOperationException ex)
        {
            // Log and continue - this can happen when package.json is missing
            this.Logger.LogInformation(ex, "Could not process {ComponentLocation} file.", file.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogInformation(e, "Could not process {ComponentLocation} file.", file.Location);
        }
    }

    protected TypedComponent? CreateComponent(string name, string? version, string? integrity)
    {
        return NpmComponentUtilities.GetTypedComponent(name, version, integrity, NpmRegistryHost, this.Logger);
    }

    protected void RecordComponent(
        ISingleFileComponentRecorder recorder,
        TypedComponent component,
        bool isDevDependency,
        TypedComponent? explicitReferencedDependency = null,
        string? parentComponentId = null)
    {
        NpmComponentUtilities.TraverseAndRecordComponents(
            isDevDependency,
            recorder,
            component,
            explicitReferencedDependency ?? component,
            parentComponentId);
    }

    protected void RecordComponent(
        ISingleFileComponentRecorder recorder,
        TypedComponent component,
        bool isDevDependency,
        TypedComponent explicitReferencedDependency,
        bool isExplicitReferencedDependency)
    {
        NpmComponentUtilities.AddOrUpdateDetectedComponent(
            recorder,
            component,
            isDevDependency,
            parentComponentId: null,
            isExplicitReferencedDependency);
    }

    private IObservable<ProcessRequest> RemoveNodeModuleNestedFiles(IObservable<ProcessRequest> componentStreams)
    {
        var directoryItemFacades = new List<DirectoryItemFacade>();
        var directoryItemFacadesByPath = new Dictionary<string, DirectoryItemFacade>();

        return Observable.Create<ProcessRequest>(s =>
        {
            return componentStreams.Subscribe(
                processRequest =>
                {
                    var item = processRequest.ComponentStream;
                    var currentDir = item.Location;
                    DirectoryItemFacade? last = null;
                    do
                    {
                        currentDir = this.pathUtilityService.GetParentDirectory(currentDir);

                        // We've reached the top / root
                        if (currentDir == null)
                        {
                            // If our last directory isn't in our list of top level nodes, it should be added. This happens for the first processed item and then subsequent times we have a new root (edge cases with multiple hard drives, for example)
                            if (last is not null && !directoryItemFacades.Contains(last))
                            {
                                directoryItemFacades.Add(last);
                            }

                            var skippedFolder = this.SkippedFolders.FirstOrDefault(folder => item.Location.Contains(folder));

                            // When node_modules is part of the path down to a given item, we skip the item. Otherwise, we yield the item.
                            if (string.IsNullOrEmpty(skippedFolder))
                            {
                                s.OnNext(processRequest);
                            }
                            else
                            {
                                this.Logger.LogDebug("Ignoring package-lock.json at {PackageLockJsonLocation}, as it is inside a {SkippedFolder} folder.", item.Location, skippedFolder);
                            }

                            break;
                        }

                        var directoryExisted = directoryItemFacadesByPath.TryGetValue(currentDir, out var current);
                        if (!directoryExisted)
                        {
                            directoryItemFacadesByPath[currentDir] = current = new DirectoryItemFacade
                            {
                                Name = currentDir,
                                Files = [],
                                Directories = [],
                            };
                        }

                        // If we came from a directory, we add it to our graph.
                        if (last != null)
                        {
                            current!.Directories.Add(last);
                        }

                        // If we didn't come from a directory, it's because we're just getting started. Our current directory should include the file that led to it showing up in the graph.
                        else
                        {
                            current!.Files.Add(item);
                        }

                        last = current;
                    }

                    // Go all the way up
                    while (currentDir != null);
                },
                s.OnCompleted);
        });
    }
}

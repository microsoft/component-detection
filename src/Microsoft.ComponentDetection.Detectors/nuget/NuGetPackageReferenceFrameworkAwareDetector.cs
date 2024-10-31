namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.Frameworks;
using global::NuGet.Packaging.Core;
using global::NuGet.ProjectModel;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class NuGetPackageReferenceFrameworkAwareDetector : FileComponentDetector, IExperimentalDetector
{
    public const string OmittedFrameworkComponentsTelemetryKey = "OmittedFrameworkComponents";

    public const string ProjectDependencyType = "project";

    private readonly IFileUtilityService fileUtilityService;

    public NuGetPackageReferenceFrameworkAwareDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IFileUtilityService fileUtilityService,
        ILogger<NuGetPackageReferenceFrameworkAwareDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    public override string Id { get; } = "NuGetPackageReferenceFrameworkAware";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)];

    public override IList<string> SearchPatterns { get; } = ["project.assets.json"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.NuGet];

    public override int Version { get; } = 1;

    private static void RegisterComponentWithFramework(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        DetectedComponent detectedComponent,
        bool isExplicitReferencedDependency = false,
        string parentComponentId = null,
        bool? isDevelopmentDependency = null,
        NuGetFramework targetFramework = null)
    {
        singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency, parentComponentId, isDevelopmentDependency);

        // Add framework information to the actual component
        if (targetFramework is not null)
        {
            // get the actual component in case it already exists
            detectedComponent = singleFileComponentRecorder.GetComponent(detectedComponent.Component.Id);
            ((NuGetComponent)detectedComponent.Component).TargetFrameworks.Add(targetFramework.GetShortFolderName());
        }
    }

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockFile = new LockFileFormat().Read(processRequest.ComponentStream.Stream, processRequest.ComponentStream.Location);

            this.RecordLockfileVersion(lockFile.Version);

            if (lockFile.PackageSpec == null)
            {
                throw new FormatException("Lockfile did not contain a PackageSpec");
            }

            var explicitReferencedDependencies = this.GetTopLevelLibraries(lockFile)
                .Select(x => this.GetLibraryComponentWithDependencyLookup(lockFile.Libraries, x.Name, x.Version, x.VersionRange))
                .ToList();
            var explicitlyReferencedComponentIds =
                explicitReferencedDependencies
                    .Select(x => new NuGetComponent(x.Name, x.Version.ToNormalizedString()).Id)
                    .ToHashSet();

            // Since we report projects as the location, we ignore the passed in single file recorder.
            var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(lockFile.PackageSpec.RestoreMetadata.ProjectPath);
            foreach (var target in lockFile.Targets)
            {
                var frameworkPackages = FrameworkPackages.GetFrameworkPackages(target.TargetFramework);

                // This call to GetTargetLibrary is not guarded, because if this can't be resolved then something is fundamentally broken (e.g. an explicit dependency reference not being in the list of libraries)
                // issue: we treat top level dependencies for all targets as top level for each target, but some may not be top level for other targets, or may not even be present for other targets.
                foreach (var library in explicitReferencedDependencies.Select(x => target.GetTargetLibrary(x.Name)).Where(x => x != null))
                {
                    this.NavigateAndRegister(target, explicitlyReferencedComponentIds, singleFileComponentRecorder, library, null, frameworkPackages);
                }
            }

            // Register PackageDownload
            this.RegisterPackageDownloads(singleFileComponentRecorder, lockFile);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the package
            this.Logger.LogError(e, "Failed to process NuGet lockfile {NuGetLockFile}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    private void NavigateAndRegister(
        LockFileTarget target,
        HashSet<string> explicitlyReferencedComponentIds,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        LockFileTargetLibrary library,
        string parentComponentId,
        FrameworkPackages frameworkPackages,
        HashSet<string> visited = null)
    {
        if (library.Type == ProjectDependencyType)
        {
            return;
        }

        var isFrameworkComponent = frameworkPackages?.IsAFrameworkComponent(library.Name, library.Version) ?? false;
        var isDevelopmentDependency = isFrameworkComponent || IsADevelopmentDependency(library);

        visited ??= [];

        var libraryComponent = new DetectedComponent(new NuGetComponent(library.Name, library.Version.ToNormalizedString()));
        var isExplicitReferencedDependency = explicitlyReferencedComponentIds.Contains(libraryComponent.Component.Id);
        RegisterComponentWithFramework(singleFileComponentRecorder, libraryComponent, isExplicitReferencedDependency, parentComponentId, isDevelopmentDependency, targetFramework: target.TargetFramework);

        foreach (var dependency in library.Dependencies)
        {
            if (visited.Contains(dependency.Id))
            {
                continue;
            }

            var targetLibrary = target.GetTargetLibrary(dependency.Id);
            if (targetLibrary == null)
            {
                // We have to exclude this case -- it looks like a bug in project.assets.json, but there are project.assets.json files that don't have a dependency library in the libraries set.
            }
            else
            {
                visited.Add(dependency.Id);
                this.NavigateAndRegister(target, explicitlyReferencedComponentIds, singleFileComponentRecorder, targetLibrary, libraryComponent.Component.Id, frameworkPackages, visited);
            }
        }

        // a placeholder item is an empty file that doesn't exist with name _._ meant to indicate an empty folder in a nuget package, but also used by NuGet when a package's assets are excluded.
        bool IsAPlaceholderItem(LockFileItem item) => Path.GetFileName(item.Path).Equals(PackagingCoreConstants.EmptyFolder, StringComparison.OrdinalIgnoreCase);

        // A library is development dependency if all of the runtime assemblies and runtime targets are placeholders or empty (All returns true for empty).
        bool IsADevelopmentDependency(LockFileTargetLibrary library) => library.RuntimeAssemblies.Concat(library.RuntimeTargets).All(IsAPlaceholderItem);
    }

    private void RegisterPackageDownloads(ISingleFileComponentRecorder singleFileComponentRecorder, LockFile lockFile)
    {
        foreach (var framework in lockFile.PackageSpec.TargetFrameworks)
        {
            foreach (var packageDownload in framework.DownloadDependencies)
            {
                var libraryComponent = new DetectedComponent(new NuGetComponent(packageDownload.Name, packageDownload.VersionRange.MinVersion.ToNormalizedString()));

                // PackageDownload is always a development dependency since it's usage does not make it part of the application
                RegisterComponentWithFramework(singleFileComponentRecorder, libraryComponent, isExplicitReferencedDependency: true, isDevelopmentDependency: true, targetFramework: framework.FrameworkName);
            }
        }
    }

    private List<(string Name, Version Version, VersionRange VersionRange, bool IsPackageDownload)> GetTopLevelLibraries(LockFile lockFile)
    {
        // First, populate libraries from the TargetFrameworks section -- This is the base level authoritative list of nuget packages a project has dependencies on.
        var toBeFilled = new List<(string Name, Version Version, VersionRange VersionRange, bool IsPackageDownload)>();

        foreach (var framework in lockFile.PackageSpec.TargetFrameworks)
        {
            foreach (var dependency in framework.Dependencies)
            {
                toBeFilled.Add((dependency.Name, Version: null, dependency.LibraryRange.VersionRange, false));
            }
        }

        // Next, we need to resolve project references -- This is a little funky, because project references are only stored via path in
        //  project.assets.json, so we first build a list of all paths and then compare what is top level to them to resolve their
        //  associated library.
        var projectDirectory = Path.GetDirectoryName(lockFile.PackageSpec.RestoreMetadata.ProjectPath);
        var librariesWithAbsolutePath =
            lockFile.Libraries.Where(x => x.Type == ProjectDependencyType)
                .Select(x => (library: x, absoluteProjectPath: Path.GetFullPath(Path.Combine(projectDirectory, x.Path))))
                .ToDictionary(x => x.absoluteProjectPath, x => x.library);

        foreach (var restoreMetadataTargetFramework in lockFile.PackageSpec.RestoreMetadata.TargetFrameworks)
        {
            foreach (var projectReference in restoreMetadataTargetFramework.ProjectReferences)
            {
                if (librariesWithAbsolutePath.TryGetValue(Path.GetFullPath(projectReference.ProjectPath), out var library))
                {
                    toBeFilled.Add((library.Name, library.Version.Version, null, false));
                }
            }
        }

        return toBeFilled;
    }

    // Looks up a library in project.assets.json given a version (preferred) or version range (have to in some cases due to how project.assets.json stores things)
    private LockFileLibrary GetLibraryComponentWithDependencyLookup(IList<LockFileLibrary> libraries, string dependencyId, Version version, VersionRange versionRange)
    {
        if ((version == null && versionRange == null) || (version != null && versionRange != null))
        {
            throw new ArgumentException($"Either {nameof(version)} or {nameof(versionRange)} must be specified, but not both.");
        }

        var matchingLibraryNames = libraries.Where(x => string.Equals(x.Name, dependencyId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matchingLibraryNames.Count == 0)
        {
            throw new InvalidOperationException("Project.assets.json is malformed, no library could be found matching: " + dependencyId);
        }

        LockFileLibrary matchingLibrary;
        if (version != null)
        {
            // .Version.Version ensures we get to a nuget normalized 4 part version
            matchingLibrary = matchingLibraryNames.FirstOrDefault(x => x.Version.Version.Equals(version));
        }
        else
        {
            matchingLibrary = matchingLibraryNames.FirstOrDefault(x => versionRange.Satisfies(x.Version));
        }

        if (matchingLibrary == null)
        {
            matchingLibrary = matchingLibraryNames.First();
            var versionString = versionRange != null ? versionRange.ToNormalizedString() : version.ToString();
            this.Logger.LogWarning(
                "Couldn't satisfy lookup for {Version}. Falling back to first found component for {MatchingLibraryName}, resolving to version {MatchingLibraryVersion}.",
                versionString,
                matchingLibrary.Name,
                matchingLibrary.Version);
        }

        return matchingLibrary;
    }
}

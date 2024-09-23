namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

using Task = System.Threading.Tasks.Task;

public class NuGetMSBuildBinaryLogComponentDetector : FileComponentDetector
{
    private static readonly HashSet<string> TopLevelPackageItemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PackageReference",
    };

    // the items listed below represent collection names that NuGet will resolve a package into, along with the metadata value names to get the package name and version
    private static readonly Dictionary<string, (string NameMetadata, string VersionMetadata)> ResolvedPackageItemNames = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        // regular restore operations
        ["NativeCopyLocalItems"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["ResourceCopyLocalItems"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["RuntimeCopyLocalItems"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["ResolvedAnalyzers"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["_PackageDependenciesDesignTime"] = ("Name", "Version"),

        // implicitly added by the SDK during a publish operation
        ["ResolvedAppHostPack"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["ResolvedSingleFileHostPack"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["ResolvedComHostPack"] = ("NuGetPackageId", "NuGetPackageVersion"),
        ["ResolvedIjwHostPack"] = ("NuGetPackageId", "NuGetPackageVersion"),
    };

    private static readonly object MSBuildRegistrationGate = new();

    public NuGetMSBuildBinaryLogComponentDetector(
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<NuGetMSBuildBinaryLogComponentDetector> logger)
    {
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "NuGetMSBuildBinaryLog";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "*.binlog", "*.buildlog" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.NuGet };

    public override int Version { get; } = 1;

    private static void ProcessResolvedPackageReference(Dictionary<string, HashSet<string>> topLevelDependencies, Dictionary<string, Dictionary<string, HashSet<string>>> projectResolvedDependencies, NamedNode node)
    {
        var doRemoveOperation = node is RemoveItem;
        var doAddOperation = node is AddItem;
        if (TopLevelPackageItemNames.Contains(node.Name))
        {
            var projectEvaluation = node.GetNearestParent<ProjectEvaluation>();
            if (projectEvaluation is not null)
            {
                foreach (var child in node.Children.OfType<Item>())
                {
                    var packageName = child.Name;
                    if (!topLevelDependencies.TryGetValue(projectEvaluation.ProjectFile, out var topLevel))
                    {
                        topLevel = new(StringComparer.OrdinalIgnoreCase);
                        topLevelDependencies[projectEvaluation.ProjectFile] = topLevel;
                    }

                    if (doRemoveOperation)
                    {
                        topLevel.Remove(packageName);
                    }

                    if (doAddOperation)
                    {
                        topLevel.Add(packageName);
                    }
                }
            }
        }
        else if (ResolvedPackageItemNames.TryGetValue(node.Name, out var metadataNames))
        {
            var nameMetadata = metadataNames.NameMetadata;
            var versionMetadata = metadataNames.VersionMetadata;
            var originalProject = node.GetNearestParent<Project>();
            if (originalProject is not null)
            {
                foreach (var child in node.Children.OfType<Item>())
                {
                    var packageName = GetChildMetadataValue(child, nameMetadata);
                    var packageVersion = GetChildMetadataValue(child, versionMetadata);
                    if (packageName is not null && packageVersion is not null)
                    {
                        var project = originalProject;
                        while (project is not null)
                        {
                            if (!projectResolvedDependencies.TryGetValue(project.ProjectFile, out var projectDependencies))
                            {
                                projectDependencies = new(StringComparer.OrdinalIgnoreCase);
                                projectResolvedDependencies[project.ProjectFile] = projectDependencies;
                            }

                            if (!projectDependencies.TryGetValue(packageName, out var packageVersions))
                            {
                                packageVersions = new(StringComparer.OrdinalIgnoreCase);
                                projectDependencies[packageName] = packageVersions;
                            }

                            if (doRemoveOperation)
                            {
                                packageVersions.Remove(packageVersion);
                            }

                            if (doAddOperation)
                            {
                                packageVersions.Add(packageVersion);
                            }

                            project = project.GetNearestParent<Project>();
                        }
                    }
                }
            }
        }
    }

    private static string GetChildMetadataValue(TreeNode node, string metadataItemName)
    {
        var metadata = node.Children.OfType<Metadata>();
        var metadataValue = metadata.FirstOrDefault(m => m.Name.Equals(metadataItemName, StringComparison.OrdinalIgnoreCase))?.Value;
        return metadataValue;
    }

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureMSBuildIsRegistered();

            var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(processRequest.ComponentStream.Location);
            var extension = Path.GetExtension(processRequest.ComponentStream.Location);
            var buildRoot = extension.ToLower() switch
            {
                ".binlog" => BinaryLog.ReadBuild(processRequest.ComponentStream.Stream),
                ".buildlog" => BuildLogReader.Read(processRequest.ComponentStream.Stream),
                _ => throw new NotSupportedException($"Unexpected log file extension: {extension}"),
            };
            this.RecordLockfileVersion(buildRoot.FileFormatVersion);
            this.ProcessBinLog(buildRoot, singleFileComponentRecorder);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the package
            this.Logger.LogError(e, "Failed to process MSBuild binary log {BinLogFile}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    internal static void EnsureMSBuildIsRegistered()
    {
        lock (MSBuildRegistrationGate)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                // this must happen once per process, and never again
                var defaultInstance = MSBuildLocator.QueryVisualStudioInstances().First();
                MSBuildLocator.RegisterInstance(defaultInstance);
            }
        }
    }

    protected override Task OnDetectionFinishedAsync()
    {
        return Task.CompletedTask;
    }

    private void ProcessBinLog(Build buildRoot, ISingleFileComponentRecorder componentRecorder)
    {
        // maps a project path to a set of resolved dependencies
        var projectTopLevelDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var projectResolvedDependencies = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        buildRoot.VisitAllChildren<BaseNode>(node =>
        {
            switch (node)
            {
                case NamedNode namedNode when namedNode is AddItem or RemoveItem:
                    ProcessResolvedPackageReference(projectTopLevelDependencies, projectResolvedDependencies, namedNode);
                    break;
                default:
                    break;
            }
        });

        // dependencies were resolved per project, we need to re-arrange them to be per package/version
        var projectsPerPackage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in projectResolvedDependencies.Keys.OrderBy(p => p))
        {
            if (Path.GetExtension(projectPath).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                // don't report solution files
                continue;
            }

            var projectDependencies = projectResolvedDependencies[projectPath];
            foreach (var (packageName, packageVersions) in projectDependencies.OrderBy(p => p.Key))
            {
                foreach (var packageVersion in packageVersions.OrderBy(v => v))
                {
                    var key = $"{packageName}/{packageVersion}";
                    if (!projectsPerPackage.TryGetValue(key, out var projectPaths))
                    {
                        projectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        projectsPerPackage[key] = projectPaths;
                    }

                    projectPaths.Add(projectPath);
                }
            }
        }

        // report it all
        foreach (var packageNameAndVersion in projectsPerPackage.Keys.OrderBy(p => p))
        {
            var projectPaths = projectsPerPackage[packageNameAndVersion];
            var parts = packageNameAndVersion.Split('/', 2);
            var packageName = parts[0];
            var packageVersion = parts[1];
            var component = new NuGetComponent(packageName, packageVersion);
            var libraryComponent = new DetectedComponent(component);
            foreach (var projectPath in projectPaths)
            {
                libraryComponent.FilePaths.Add(projectPath);
            }

            componentRecorder.RegisterUsage(libraryComponent);
        }
    }
}

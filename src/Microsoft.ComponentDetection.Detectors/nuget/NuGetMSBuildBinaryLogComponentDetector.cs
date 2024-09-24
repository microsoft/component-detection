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

    // the items listed below represent top-level property names that correspond to well-known components
    private static readonly Dictionary<string, string> ComponentPropertyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NETCoreSdkVersion"] = ".NET SDK",
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

    private static void ProcessResolvedComponentReference(ProjectPathToTopLevelComponents topLevelComponents, ProjectPathToComponents projectResolvedComponents, NamedNode node)
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
                    var componentName = child.Name;
                    var topLevel = topLevelComponents.GetComponentNames(projectEvaluation.ProjectFile);

                    if (doRemoveOperation)
                    {
                        topLevel.Remove(componentName);
                    }

                    if (doAddOperation)
                    {
                        topLevel.Add(componentName);
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
                    var componentName = GetChildMetadataValue(child, nameMetadata);
                    var componentVersion = GetChildMetadataValue(child, versionMetadata);
                    if (componentName is not null && componentVersion is not null)
                    {
                        var project = originalProject;
                        while (project is not null)
                        {
                            var components = projectResolvedComponents.GetComponents(project.ProjectFile);
                            var evaluatedVersions = components.GetEvaluatedVersions(componentName);
                            var componentVersions = evaluatedVersions.GetComponentVersions(project.EvaluationId);

                            if (doRemoveOperation)
                            {
                                componentVersions.Remove(componentVersion);
                            }

                            if (doAddOperation)
                            {
                                componentVersions.Add(componentVersion);
                            }

                            project = project.GetNearestParent<Project>();
                        }
                    }
                }
            }
        }
    }

    private static void ProcessProjectProperty(ProjectPathToComponents projectResolvedComponents, Property node)
    {
        if (ComponentPropertyNames.TryGetValue(node.Name, out var packageName))
        {
            string projectFile;
            int evaluationId;
            var projectEvaluation = node.GetNearestParent<ProjectEvaluation>();
            if (projectEvaluation is not null)
            {
                // `.binlog` files store properties in a `ProjectEvaluation`
                projectFile = projectEvaluation.ProjectFile;
                evaluationId = projectEvaluation.Id;
            }
            else
            {
                // `.buildlog` files store proeprties in `Project`
                var project = node.GetNearestParent<Project>();
                projectFile = project?.ProjectFile;
                evaluationId = project?.EvaluationId ?? 0;
            }

            if (projectFile is not null)
            {
                var componentVersion = node.Value;
                var components = projectResolvedComponents.GetComponents(projectFile);
                var evaluatedVersions = components.GetEvaluatedVersions(packageName);
                var componentVersions = evaluatedVersions.GetComponentVersions(evaluationId);

                componentVersions.Add(componentVersion);
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
        var projectTopLevelComponents = new ProjectPathToTopLevelComponents();
        var projectResolvedComponents = new ProjectPathToComponents();
        buildRoot.VisitAllChildren<BaseNode>(node =>
        {
            switch (node)
            {
                case NamedNode namedNode when namedNode is AddItem or RemoveItem:
                    ProcessResolvedComponentReference(projectTopLevelComponents, projectResolvedComponents, namedNode);
                    break;
                case Property property when property.Parent is Folder folder && folder.Name == "Properties":
                    ProcessProjectProperty(projectResolvedComponents, property);
                    break;
                default:
                    break;
            }
        });

        // dependencies were resolved per project, we need to re-arrange them to be per package/version
        var projectsPerComponent = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in projectResolvedComponents.Keys.OrderBy(p => p))
        {
            if (Path.GetExtension(projectPath).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                // don't report solution files
                continue;
            }

            var projectComponents = projectResolvedComponents[projectPath];
            foreach (var (componentName, componentVersionsPerEvaluationid) in projectComponents.OrderBy(p => p.Key))
            {
                foreach (var componentVersions in componentVersionsPerEvaluationid.OrderBy(p => p.Key).Select(kvp => kvp.Value))
                {
                    foreach (var componentVersion in componentVersions.OrderBy(v => v))
                    {
                        var key = $"{componentName}/{componentVersion}";
                        if (!projectsPerComponent.TryGetValue(key, out var projectPaths))
                        {
                            projectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            projectsPerComponent[key] = projectPaths;
                        }

                        projectPaths.Add(projectPath);
                    }
                }
            }
        }

        // report it all
        foreach (var componentNameAndVersion in projectsPerComponent.Keys.OrderBy(p => p))
        {
            var projectPaths = projectsPerComponent[componentNameAndVersion];
            var parts = componentNameAndVersion.Split('/', 2);
            var componentName = parts[0];
            var componentVersion = parts[1];
            var component = new NuGetComponent(componentName, componentVersion);
            var libraryComponent = new DetectedComponent(component);
            foreach (var projectPath in projectPaths)
            {
                libraryComponent.FilePaths.Add(projectPath);
            }

            componentRecorder.RegisterUsage(libraryComponent);
        }
    }

    // To make the above code easier to read, some helper types are added here.  Without these, the code above would contain a type of:
    //   Dictionary<string, Dictionary<string, Dictionary<int, HashSet<string>>>>
    // which isn't very descriptive.
    private abstract class KeyedCollection<TKey, TValue> : Dictionary<TKey, TValue>
        where TKey : notnull
    {
        protected KeyedCollection()
            : base()
        {
        }

        protected KeyedCollection(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {
        }

        protected TValue GetOrAdd(TKey key, Func<TValue> valueFactory)
        {
            if (!this.TryGetValue(key, out var value))
            {
                value = valueFactory();
                this[key] = value;
            }

            return value;
        }
    }

    // Represents a collection of top-level components for a given project path.
    private class ProjectPathToTopLevelComponents : KeyedCollection<string, HashSet<string>>
    {
        public HashSet<string> GetComponentNames(string projectPath) => this.GetOrAdd(projectPath, () => new(StringComparer.OrdinalIgnoreCase));
    }

    // Represents a collection of evaluated components for a given project path.
    private class ProjectPathToComponents : KeyedCollection<string, ComponentNameToEvaluatedVersions>
    {
        public ProjectPathToComponents()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public ComponentNameToEvaluatedVersions GetComponents(string projectPath) => this.GetOrAdd(projectPath, () => new());
    }

    // Represents a collection of evaluated components for a given component name.
    private class ComponentNameToEvaluatedVersions : KeyedCollection<string, EvaluationIdToComponentVersions>
    {
        public ComponentNameToEvaluatedVersions()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public EvaluationIdToComponentVersions GetEvaluatedVersions(string componentName) => this.GetOrAdd(componentName, () => new());
    }

    // Represents a collection of component versions for a given evaluation id.
    private class EvaluationIdToComponentVersions : KeyedCollection<int, ComponentVersions>
    {
        public ComponentVersions GetComponentVersions(int evaluationId) => this.GetOrAdd(evaluationId, () => new());
    }

    // Represents a collection of version strings.
    private class ComponentVersions : HashSet<string>
    {
        public ComponentVersions()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}

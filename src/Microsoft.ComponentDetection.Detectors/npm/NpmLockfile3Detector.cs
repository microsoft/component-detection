#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Npm;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

public class NpmLockfile3Detector : NpmLockfileDetectorBase
{
    private static readonly string NodeModules = NpmComponentUtilities.NodeModules;

    public NpmLockfile3Detector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPathUtilityService pathUtilityService,
        ILogger<NpmLockfile3Detector> logger)
        : base(
            componentStreamEnumerableFactory,
            walkerFactory,
            pathUtilityService,
            logger)
    {
    }

    public NpmLockfile3Detector(IPathUtilityService pathUtilityService)
        : base(pathUtilityService)
    {
    }

    public override string Id => "NpmLockfile3";

    public override int Version => 2;

    protected override bool IsSupportedLockfileVersion(int lockfileVersion) => lockfileVersion == 3;

    protected override JToken ResolveDependencyObject(JToken packageLockJToken) => packageLockJToken["packages"];

    protected override bool TryEnqueueFirstLevelDependencies(
        Queue<(JProperty DependencyProperty, TypedComponent ParentComponent)> queue,
        JToken dependencies,
        IDictionary<string, JProperty> dependencyLookup,
        TypedComponent parentComponent = null,
        bool skipValidation = false)
    {
        if (dependencies == null)
        {
            return true;
        }

        var isValid = true;

        foreach (var dependency in dependencies.Cast<JProperty>())
        {
            if (dependency?.Name == null)
            {
                continue;
            }

            var inLock = dependencyLookup.TryGetValue($"{NodeModules}/{dependency.Name}", out var dependencyProperty);
            if (inLock)
            {
                queue.Enqueue((dependencyProperty, parentComponent));
            }
            else if (skipValidation)
            {
            }
            else
            {
                isValid = false;
            }
        }

        return isValid;
    }

    protected override void EnqueueAllDependencies(
        IDictionary<string, JProperty> dependencyLookup,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        Queue<(JProperty CurrentSubDependency, TypedComponent ParentComponent)> subQueue,
        JProperty currentDependency,
        TypedComponent typedComponent) =>
        this.TryEnqueueFirstLevelDependenciesLockfile3(
            subQueue,
            currentDependency.Value["dependencies"],
            dependencyLookup,
            singleFileComponentRecorder,
            parentComponent: typedComponent);

    private void TryEnqueueFirstLevelDependenciesLockfile3(
        Queue<(JProperty DependencyProperty, TypedComponent ParentComponent)> queue,
        JToken dependencies,
        IDictionary<string, JProperty> dependencyLookup,
        ISingleFileComponentRecorder componentRecorder,
        TypedComponent parentComponent)
    {
        if (dependencies == null)
        {
            return;
        }

        foreach (var dependency in dependencies.Cast<JProperty>())
        {
            if (dependency?.Name == null)
            {
                continue;
            }

            // First, check if there is an entry in the lockfile for this dependency nested in its ancestors
            var ancestors = componentRecorder.DependencyGraph.GetAncestors(parentComponent.Id);
            ancestors.Add(parentComponent.Id);

            // remove version information
            ancestors = ancestors.Select(x => x.Split(' ')[0]).ToList();

            var possibleDepPaths = ancestors
                .Select((t, i) => ancestors.TakeLast(ancestors.Count - i)); // depth-first search

            var inLock = false;
            JProperty dependencyProperty;
            foreach (var possibleDepPath in possibleDepPaths)
            {
                var ancestorNodeModulesPath = string.Format(
                    "{0}/{1}/{0}/{2}",
                    NodeModules,
                    string.Join($"/{NodeModules}/", possibleDepPath),
                    dependency.Name);

                // Does this exist?
                inLock = dependencyLookup.TryGetValue(ancestorNodeModulesPath, out dependencyProperty);

                if (!inLock)
                {
                    continue;
                }

                this.Logger.LogDebug("Found nested dependency {Dependency} in {AncestorNodeModulesPath}", dependency.Name, ancestorNodeModulesPath);
                queue.Enqueue((dependencyProperty, parentComponent));
                break;
            }

            if (inLock)
            {
                continue;
            }

            // If not, check if there is an entry in the lockfile for this dependency at the top level
            inLock = dependencyLookup.TryGetValue($"{NodeModules}/{dependency.Name}", out dependencyProperty);
            if (inLock)
            {
                queue.Enqueue((dependencyProperty, parentComponent));
            }
            else
            {
                this.Logger.LogWarning("Could not find dependency {Dependency} in lockfile", dependency.Name);
            }
        }
    }
}

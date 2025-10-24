#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Npm;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

public class NpmComponentDetectorWithRoots : NpmLockfileDetectorBase
{
    public NpmComponentDetectorWithRoots(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IPathUtilityService pathUtilityService,
        ILogger<NpmComponentDetectorWithRoots> logger)
        : base(
            componentStreamEnumerableFactory,
            walkerFactory,
            pathUtilityService,
            logger)
    {
    }

    public NpmComponentDetectorWithRoots(IPathUtilityService pathUtilityService)
        : base(pathUtilityService)
    {
    }

    public override string Id => "NpmWithRoots";

    public override int Version => 3;

    protected override bool IsSupportedLockfileVersion(int lockfileVersion) => lockfileVersion != 3;

    protected override JToken ResolveDependencyObject(JToken packageLockJToken) => packageLockJToken["dependencies"];

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

            var inLock = dependencyLookup.TryGetValue(dependency.Name, out var dependencyProperty);
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
        TypedComponent typedComponent)
    {
        this.EnqueueDependencies(subQueue, currentDependency.Value["dependencies"], parentComponent: typedComponent);
        this.TryEnqueueFirstLevelDependencies(subQueue, currentDependency.Value["requires"], dependencyLookup, parentComponent: typedComponent);
    }

    private void EnqueueDependencies(Queue<(JProperty Dependency, TypedComponent ParentComponent)> queue, JToken dependencies, TypedComponent parentComponent)
    {
        if (dependencies == null)
        {
            return;
        }

        foreach (var dependency in dependencies.Cast<JProperty>())
        {
            if (dependency != null)
            {
                queue.Enqueue((dependency, parentComponent));
            }
        }
    }
}

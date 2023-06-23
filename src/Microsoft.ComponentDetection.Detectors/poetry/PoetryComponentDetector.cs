namespace Microsoft.ComponentDetection.Detectors.Poetry;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry.Contracts;
using Microsoft.Extensions.Logging;
using Tomlyn;

public class PoetryComponentDetector : FileComponentDetector, IExperimentalDetector
{
    public PoetryComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<PoetryComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "Poetry";

    public override IList<string> SearchPatterns { get; } = new List<string> { "poetry.lock" };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Pip };

    public override int Version { get; } = 3;

    public override IEnumerable<string> Categories => new List<string> { "Python" };

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var poetryLockFile = processRequest.ComponentStream;
        this.Logger.LogDebug("Found Poetry lockfile {PoetryLockFile}", poetryLockFile);

        var reader = new StreamReader(poetryLockFile.Stream);
        var options = new TomlModelOptions
        {
            IgnoreMissingProperties = true,
        };
        var poetryLock = Toml.ToModel<PoetryLock>(await reader.ReadToEndAsync(), options: options);
        poetryLock.Package.ToList().ForEach(package =>
        {
            var isDevelopmentDependency = package.Category != "main";

            if (package.Source != null && package.Source.Type == "git")
            {
                var component = new DetectedComponent(new GitComponent(new Uri(package.Source.Url), package.Source.ResolvedReference));
                singleFileComponentRecorder.RegisterUsage(component, isExplicitReferencedDependency: true, isDevelopmentDependency: isDevelopmentDependency);
            }
            else
            {
                var component = new DetectedComponent(new PipComponent(package.Name, package.Version));
                singleFileComponentRecorder.RegisterUsage(component, isExplicitReferencedDependency: true, isDevelopmentDependency: isDevelopmentDependency);
            }
        });
        await Task.CompletedTask;
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Poetry;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

    public override IList<string> SearchPatterns { get; } = ["poetry.lock"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Pip];

    public override int Version { get; } = 3;

    public override IEnumerable<string> Categories => ["Python"];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var poetryLockFile = processRequest.ComponentStream;
        this.Logger.LogDebug("Found Poetry lockfile {PoetryLockFile}", poetryLockFile);

        var reader = new StreamReader(poetryLockFile.Stream);
        var options = new TomlModelOptions
        {
            IgnoreMissingProperties = true,
        };
        var poetryLock = Toml.ToModel<PoetryLock>(await reader.ReadToEndAsync(cancellationToken), options: options);

        if (poetryLock.Metadata != null && poetryLock.Metadata.TryGetValue("lock-version", out var lockVersion))
        {
            this.RecordLockfileVersion(lockVersion.ToString());
        }

        poetryLock.Package.ToList().ForEach(package =>
        {
            if (package.Source != null && package.Source.Type == "git")
            {
                var component = new DetectedComponent(new GitComponent(new Uri(package.Source.Url), package.Source.ResolvedReference));
                singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: false);
            }
            else
            {
                var component = new DetectedComponent(new PipComponent(package.Name, package.Version));
                singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: false);
            }
        });
        await Task.CompletedTask;
    }
}

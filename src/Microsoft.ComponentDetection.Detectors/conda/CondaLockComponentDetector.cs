#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Poetry;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.CondaLock;
using Microsoft.ComponentDetection.Detectors.CondaLock.Contracts;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

public class CondaLockComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public CondaLockComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<CondaLockComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "CondaLock";

    public override IList<string> SearchPatterns { get; } = ["conda-lock.yml", "*.conda-lock.yml"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Conda, ComponentType.Pip];

    public override int Version { get; } = 2;

    public override IEnumerable<string> Categories => ["Python"];

    /// <inheritdoc/>
    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;

        this.Logger.LogDebug("Found conda-lock file: {YamlFile}", processRequest.ComponentStream.Location);
        try
        {
            // Parse conda lock file
            var condaLock = this.ParseCondaLock(processRequest);
            this.RecordLockfileVersion(condaLock.Version);

            // Register the full dependency graph
            CondaDependencyResolver.RecordDependencyGraphFromFile(condaLock, singleFileComponentRecorder);
            CondaDependencyResolver.UpdateDirectlyReferencedPackages(singleFileComponentRecorder);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read conda-lock file {File}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses the conda lock yaml file.
    /// </summary>
    /// <param name="processRequest">The processRequest.</param>
    /// <returns>An instance of CondaLock.</returns>
    private CondaLock ParseCondaLock(ProcessRequest processRequest)
    {
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        return deserializer.Deserialize<CondaLock>(new StreamReader(processRequest.ComponentStream.Stream));
    }
}

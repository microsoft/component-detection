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

    public override IList<string> SearchPatterns { get; } = new List<string> { "conda-lock.yml", "*.conda-lock.yml" };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Conda, ComponentType.Pip };

    public override int Version { get; } = 1;

    public override IEnumerable<string> Categories => new List<string> { "Python" };

    /// <inheritdoc/>
    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;

        this.Logger.LogDebug("Found cond-lock file: {YamlFile}", processRequest.ComponentStream.Location);
        try
        {
            // Parse conda lock file
            var condaLock = this.ParseCondaLock(processRequest);

            // Parse conda environments to get explicit dependencies
            var explicitDependencies = new List<string>();
            if (condaLock != null && condaLock.Metadata != null && condaLock.Metadata.Sources != null)
            {
                foreach (var environment in condaLock.Metadata.Sources)
                {
                    var environmentFilePath = Path.Combine(processRequest.ComponentStream.Location, "..", environment);
                    explicitDependencies.AddRange(this.ParseExplicitDependencies(environmentFilePath));
                }
            }

            // Register the full dependency graph
            CondaDependencyResolver.RecordDependencyGraphFromFile(condaLock, explicitDependencies, singleFileComponentRecorder);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read conda yaml file {File}", processRequest.ComponentStream.Location);
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

    /// <summary>
    /// Parses a conda environment yaml file and returns a list of all dependencies explicitly listed in the file.
    /// </summary>
    /// <param name="environmentFilePath">The path to the conda environment yaml file.</param>
    /// <returns>A list of all dependencies explicitly listed in the conda environment.</returns>
    private List<string> ParseExplicitDependencies(string environmentFilePath)
    {
        var explicitDependencies = new List<string>();

        if (!File.Exists(environmentFilePath))
        {
            return explicitDependencies;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            var condaEnvironment = deserializer.Deserialize<CondaEnvironment>(new StreamReader(environmentFilePath));
            foreach (var item in condaEnvironment.Dependencies)
            {
                if (item is string)
                {
                    // Add all conda dependencies
                    explicitDependencies.Add(item.ToString());
                }
                else if (item is Dictionary<object, object> pipDependencies)
                {
                    // Add all pip dependencies
                    if (pipDependencies.First().Key.ToString() == "pip" &&
                        pipDependencies.First().Value is List<object> dependencies)
                    {
                        dependencies.ForEach(pipDependency => explicitDependencies.Add(pipDependency.ToString()));
                    }
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read conda environment yaml file {File}", environmentFilePath);
        }

        return explicitDependencies;
    }
}

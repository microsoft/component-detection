#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux.Factories;
using Microsoft.ComponentDetection.Detectors.Linux.Filters;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scanner for Linux container layers using Syft.
/// </summary>
public class LinuxScanner : ILinuxScanner
{
    private const string ScannerImage =
        "governancecontainerregistry.azurecr.io/syft:v1.37.0@sha256:48d679480c6d272c1801cf30460556959c01d4826795be31d4fd8b53750b7d91";

    private static readonly IList<string> CmdParameters =
    [
        "--quiet",
        "--output",
        "json",
    ];

    private static readonly IList<string> ScopeAllLayersParameter = ["--scope", "all-layers"];

    private static readonly IList<string> ScopeSquashedParameter = ["--scope", "squashed"];

    private static readonly SemaphoreSlim ContainerSemaphore = new SemaphoreSlim(2);

    private static readonly int SemaphoreTimeout = Convert.ToInt32(
        TimeSpan.FromHours(1).TotalMilliseconds
    );

    private readonly IDockerService dockerService;
    private readonly ILogger<LinuxScanner> logger;
    private readonly IEnumerable<IArtifactComponentFactory> componentFactories;
    private readonly IEnumerable<IArtifactFilter> artifactFilters;
    private readonly Dictionary<string, IArtifactComponentFactory> factoryLookup;
    private readonly Dictionary<
        ComponentType,
        IArtifactComponentFactory
    > componentTypeToFactoryLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxScanner"/> class.
    /// </summary>
    /// <param name="dockerService">The docker service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="componentFactories">The component factories.</param>
    /// <param name="artifactFilters">The artifact filters.</param>
    public LinuxScanner(
        IDockerService dockerService,
        ILogger<LinuxScanner> logger,
        IEnumerable<IArtifactComponentFactory> componentFactories,
        IEnumerable<IArtifactFilter> artifactFilters
    )
    {
        this.dockerService = dockerService;
        this.logger = logger;
        this.componentFactories = componentFactories;
        this.artifactFilters = artifactFilters;

        // Build a lookup dictionary for quick factory access by artifact type
        this.factoryLookup = [];
        foreach (var factory in componentFactories)
        {
            foreach (var artifactType in factory.SupportedArtifactTypes)
            {
                this.factoryLookup[artifactType] = factory;
            }
        }

        // Build a lookup dictionary for component type to factory mapping
        this.componentTypeToFactoryLookup = new Dictionary<ComponentType, IArtifactComponentFactory>
        {
            {
                ComponentType.Linux,
                componentFactories.FirstOrDefault(f => f is LinuxComponentFactory)
            },
            { ComponentType.Npm, componentFactories.FirstOrDefault(f => f is NpmComponentFactory) },
            { ComponentType.Pip, componentFactories.FirstOrDefault(f => f is PipComponentFactory) },
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LayerMappedLinuxComponents>> ScanLinuxAsync(
        string imageHash,
        IEnumerable<DockerLayer> containerLayers,
        int baseImageLayerCount,
        ISet<ComponentType> enabledComponentTypes,
        LinuxScannerScope scope,
        CancellationToken cancellationToken = default
    )
    {
        using var record = new LinuxScannerTelemetryRecord
        {
            ImageToScan = imageHash,
            ScannerVersion = ScannerImage,
        };

        var acquired = false;
        var stdout = string.Empty;
        var stderr = string.Empty;

        var scopeParameters = scope switch
        {
            LinuxScannerScope.AllLayers => ScopeAllLayersParameter,
            LinuxScannerScope.Squashed => ScopeSquashedParameter,
            _ => throw new ArgumentOutOfRangeException(
                    nameof(scope),
                    $"Unsupported scope value: {scope}"
                ),
        };

        using var syftTelemetryRecord = new LinuxScannerSyftTelemetryRecord();

        try
        {
            acquired = await ContainerSemaphore.WaitAsync(SemaphoreTimeout, cancellationToken);
            if (acquired)
            {
                try
                {
                    var command = new List<string> { imageHash }
                        .Concat(CmdParameters)
                        .Concat(scopeParameters)
                        .ToList();
                    (stdout, stderr) = await this.dockerService.CreateAndRunContainerAsync(
                        ScannerImage,
                        command,
                        cancellationToken
                    );
                }
                catch (Exception e)
                {
                    syftTelemetryRecord.Exception = JsonSerializer.Serialize(e);
                    this.logger.LogError(e, "Failed to run syft");
                    throw;
                }
            }
            else
            {
                record.SemaphoreFailure = true;
                this.logger.LogWarning(
                    "Failed to enter the container semaphore for image {ImageHash}",
                    imageHash
                );
            }
        }
        finally
        {
            if (acquired)
            {
                ContainerSemaphore.Release();
            }
        }

        record.ScanStdErr = stderr;
        record.ScanStdOut = stdout;

        if (string.IsNullOrWhiteSpace(stdout) || !string.IsNullOrWhiteSpace(stderr))
        {
            throw new InvalidOperationException(
                $"Scan failed with exit info: {stdout}{System.Environment.NewLine}{stderr}"
            );
        }

        var layerDictionary = containerLayers
            .DistinctBy(layer => layer.DiffId)
            .ToDictionary(layer => layer.DiffId, _ => new List<TypedComponent>());

        try
        {
            var syftOutput = SyftOutput.FromJson(stdout);

            // Apply artifact filters (e.g., Mariner 2.0 workaround)
            var validArtifacts = syftOutput.Artifacts.AsEnumerable();
            foreach (var filter in this.artifactFilters)
            {
                validArtifacts = filter.Filter(validArtifacts, syftOutput.Distro);
            }

            // Build a set of enabled factories based on requested component types
            var enabledFactories = new HashSet<IArtifactComponentFactory>();
            foreach (var componentType in enabledComponentTypes)
            {
                if (
                    this.componentTypeToFactoryLookup.TryGetValue(componentType, out var factory)
                    && factory != null
                )
                {
                    enabledFactories.Add(factory);
                }
            }

            // Create components using only enabled factories
            var componentsWithLayers = validArtifacts
                .DistinctBy(artifact => (artifact.Name, artifact.Version, artifact.Type))
                .Select(artifact =>
                    this.CreateComponentWithLayers(artifact, syftOutput.Distro, enabledFactories)
                )
                .Where(result => result.Component != null)
                .ToList();

            // Track unsupported artifact types for telemetry
            var unsupportedTypes = validArtifacts
                .Where(a => !this.factoryLookup.ContainsKey(a.Type))
                .Select(a => a.Type)
                .Distinct()
                .ToList();

            if (unsupportedTypes.Count > 0)
            {
                this.logger.LogDebug(
                    "Encountered unsupported artifact types: {UnsupportedTypes}",
                    string.Join(", ", unsupportedTypes)
                );
            }

            // Map components to layers
            foreach (var (component, layers) in componentsWithLayers)
            {
                layers.ToList().ForEach(layer => layerDictionary[layer].Add(component));
            }

            var layerMappedLinuxComponents = layerDictionary.Select(kvp =>
            {
                (var layerId, var components) = kvp;
                return new LayerMappedLinuxComponents
                {
                    Components = components,
                    DockerLayer = containerLayers.First(layer => layer.DiffId == layerId),
                };
            });

            // Track detected components in telemetry
            syftTelemetryRecord.Components = JsonSerializer.Serialize(
                componentsWithLayers.Select(c => c.Component.Id)
            );

            return layerMappedLinuxComponents;
        }
        catch (Exception e)
        {
            record.FailedDeserializingScannerOutput = e.ToString();
            return null;
        }
    }

    private (TypedComponent Component, IEnumerable<string> LayerIds) CreateComponentWithLayers(
        ArtifactElement artifact,
        Distro distro,
        HashSet<IArtifactComponentFactory> enabledFactories
    )
    {
        if (!this.factoryLookup.TryGetValue(artifact.Type, out var factory))
        {
            return (null, []);
        }

        // Skip this artifact if its factory is not in the enabled set
        if (!enabledFactories.Contains(factory))
        {
            return (null, []);
        }

        var component = factory.CreateComponent(artifact, distro);
        if (component == null)
        {
            return (null, []);
        }

        var layerIds = artifact.Locations?.Select(location => location.LayerId).Distinct() ?? [];
        return (component, layerIds);
    }
}

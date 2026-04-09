namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux.Factories;
using Microsoft.ComponentDetection.Detectors.Linux.Filters;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scanner for Linux container layers using Syft.
/// </summary>
internal class LinuxScanner : ILinuxScanner
{
    private static readonly IList<string> CmdParameters = ["--quiet", "--output", "json"];

    private static readonly IList<string> ScopeAllLayersParameter = ["--scope", "all-layers"];

    private static readonly IList<string> ScopeSquashedParameter = ["--scope", "squashed"];

    private readonly ILogger<LinuxScanner> logger;
    private readonly IEnumerable<IArtifactComponentFactory> componentFactories;
    private readonly IEnumerable<IArtifactFilter> artifactFilters;
    private readonly Dictionary<string, IArtifactComponentFactory> artifactTypeToFactoryLookup;
    private readonly Dictionary<
        ComponentType,
        IArtifactComponentFactory
    > componentTypeToFactoryLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxScanner"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="componentFactories">The component factories.</param>
    /// <param name="artifactFilters">The artifact filters.</param>
    public LinuxScanner(
        ILogger<LinuxScanner> logger,
        IEnumerable<IArtifactComponentFactory> componentFactories,
        IEnumerable<IArtifactFilter> artifactFilters
    )
    {
        this.logger = logger;
        this.componentFactories = componentFactories;
        this.artifactFilters = artifactFilters;

        this.artifactTypeToFactoryLookup = componentFactories
            .SelectMany(
                f => f.SupportedArtifactTypes,
                (factory, artifactType) => (artifactType, factory)
            )
            .ToDictionary(x => x.artifactType, x => x.factory);

        this.componentTypeToFactoryLookup = componentFactories.ToDictionary(
            f => f.SupportedComponentType,
            f => f
        );
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LayerMappedLinuxComponents>> ScanLinuxAsync(
        ImageReference imageReference,
        IEnumerable<DockerLayer> containerLayers,
        int baseImageLayerCount,
        ISet<ComponentType> enabledComponentTypes,
        LinuxScannerScope scope,
        ISyftRunner syftRunner,
        CancellationToken cancellationToken = default
    )
    {
        using var record = new LinuxScannerTelemetryRecord
        {
            ImageToScan = imageReference.Reference,
            ScannerVersion = DockerSyftRunner.ScannerImage,
        };
        using var syftTelemetryRecord = new LinuxScannerSyftTelemetryRecord();
        var stdout = await this.RunSyftAsync(imageReference, scope, syftRunner, record, syftTelemetryRecord, cancellationToken);

        try
        {
            var syftOutput = SyftOutput.FromJson(stdout);
            return this.ProcessSyftOutputWithTelemetry(syftOutput, containerLayers, enabledComponentTypes, syftTelemetryRecord);
        }
        catch (Exception e)
        {
            record.FailedDeserializingScannerOutput = e.ToString();
            this.logger.LogError(e, "Failed to deserialize Syft output for image {ImageReference}", imageReference.Reference);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<SyftOutput> GetSyftOutputAsync(
        ImageReference imageReference,
        LinuxScannerScope scope,
        ISyftRunner syftRunner,
        CancellationToken cancellationToken = default
    )
    {
        using var record = new LinuxScannerTelemetryRecord
        {
            ImageToScan = imageReference.Reference,
            ScannerVersion = DockerSyftRunner.ScannerImage,
        };
        using var syftTelemetryRecord = new LinuxScannerSyftTelemetryRecord();
        var stdout = await this.RunSyftAsync(imageReference, scope, syftRunner, record, syftTelemetryRecord, cancellationToken);
        try
        {
            return SyftOutput.FromJson(stdout);
        }
        catch (Exception e)
        {
            record.FailedDeserializingScannerOutput = e.ToString();
            this.logger.LogError(e, "Failed to deserialize Syft output for source {ImageReference}", imageReference.Reference);
            throw;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LayerMappedLinuxComponents> ProcessSyftOutput(
        SyftOutput syftOutput,
        IEnumerable<DockerLayer> containerLayers,
        ISet<ComponentType> enabledComponentTypes)
    {
        using var syftTelemetryRecord = new LinuxScannerSyftTelemetryRecord();
        return this.ProcessSyftOutputWithTelemetry(syftOutput, containerLayers, enabledComponentTypes, syftTelemetryRecord);
    }

    private IEnumerable<LayerMappedLinuxComponents> ProcessSyftOutputWithTelemetry(
        SyftOutput syftOutput,
        IEnumerable<DockerLayer> containerLayers,
        ISet<ComponentType> enabledComponentTypes,
        LinuxScannerSyftTelemetryRecord syftTelemetryRecord)
    {
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
            .Select(result => (Component: result.Component!, result.LayerIds))
            .ToList();

        // Track unsupported artifact types for telemetry
        var unsupportedTypes = validArtifacts
            .Where(a => !this.artifactTypeToFactoryLookup.ContainsKey(a.Type))
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

        // Track detected components in telemetry
        syftTelemetryRecord.Components = JsonSerializer.Serialize(
            componentsWithLayers.Select(c => c.Component.Id)
        );

        // Build a layer dictionary from the provided container layers and map components.
        var knownLayers = containerLayers.ToList();

        if (knownLayers.Count > 0)
        {
            var layerDictionary = knownLayers
                .DistinctBy(layer => layer.DiffId)
                .ToDictionary(layer => layer.DiffId, _ => new List<TypedComponent>());

            foreach (var (component, layers) in componentsWithLayers)
            {
                foreach (var layer in layers)
                {
                    if (layerDictionary.TryGetValue(layer, out var componentList))
                    {
                        componentList.Add(component);
                    }
                }
            }

            return layerDictionary.Select(kvp => new LayerMappedLinuxComponents
            {
                Components = kvp.Value,
                DockerLayer = knownLayers.First(layer => layer.DiffId == kvp.Key),
            });
        }

        // No container layers provided — return all components under a single
        // entry with no layer information rather than silently dropping them.
        var allComponents = componentsWithLayers.Select(c => c.Component).ToList();
        if (allComponents.Count == 0)
        {
            return [];
        }

        return
        [
            new LayerMappedLinuxComponents
            {
                Components = allComponents,
                DockerLayer = new DockerLayer()
                {
                    DiffId = string.Empty,
                    LayerIndex = 0,
                    IsBaseImage = false,
                },
            },
        ];
    }

    /// <summary>
    /// Runs the Syft scanner and returns the stdout output.
    /// </summary>
    private async Task<string> RunSyftAsync(
        ImageReference imageReference,
        LinuxScannerScope scope,
        ISyftRunner syftRunner,
        LinuxScannerTelemetryRecord record,
        LinuxScannerSyftTelemetryRecord syftTelemetryRecord,
        CancellationToken cancellationToken)
    {
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

        try
        {
            var arguments = CmdParameters
                .Concat(scopeParameters)
                .ToList();
            (stdout, stderr) = await syftRunner.RunSyftAsync(
                imageReference,
                arguments,
                cancellationToken
            );
        }
        catch (Exception e)
        {
            syftTelemetryRecord.Exception = JsonSerializer.Serialize(e);
            this.logger.LogError(e, "Failed to run syft");
            throw;
        }

        record.ScanStdErr = stderr;
        record.ScanStdOut = stdout;

        if (string.IsNullOrWhiteSpace(stdout) || !string.IsNullOrWhiteSpace(stderr))
        {
            throw new InvalidOperationException(
                $"Scan failed with exit info: {stdout}{System.Environment.NewLine}{stderr}"
            );
        }

        return stdout;
    }

    private (TypedComponent? Component, IEnumerable<string> LayerIds) CreateComponentWithLayers(
        ArtifactElement artifact,
        Distro distro,
        HashSet<IArtifactComponentFactory> enabledFactories
    )
    {
        if (!this.artifactTypeToFactoryLookup.TryGetValue(artifact.Type, out var factory))
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

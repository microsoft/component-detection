namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class LinuxScanner : ILinuxScanner
{
    private const string ScannerImage = "governancecontainerregistry.azurecr.io/syft:v0.100.0@sha256:df7b07bfadff45e0135d74f22478f47b16ac6aff4e8dbd93133fcae3bbbb790d";

    private static readonly IList<string> CmdParameters = new List<string>
    {
        "--quiet", "--scope", "all-layers", "--output", "json",
    };

    private static readonly IEnumerable<string> AllowedArtifactTypes = new[] { "apk", "deb", "rpm" };

    private static readonly SemaphoreSlim DockerSemaphore = new SemaphoreSlim(2);

    private static readonly int SemaphoreTimeout = Convert.ToInt32(TimeSpan.FromHours(1).TotalMilliseconds);

    private readonly IDockerService dockerService;
    private readonly ILogger<LinuxScanner> logger;

    public LinuxScanner(IDockerService dockerService, ILogger<LinuxScanner> logger)
    {
        this.dockerService = dockerService;
        this.logger = logger;
    }

    public async Task<IEnumerable<LayerMappedLinuxComponents>> ScanLinuxAsync(string imageHash, IEnumerable<DockerLayer> dockerLayers, int baseImageLayerCount, CancellationToken cancellationToken = default)
    {
        using var record = new LinuxScannerTelemetryRecord
        {
            ImageToScan = imageHash,
            ScannerVersion = ScannerImage,
        };

        var acquired = false;
        var stdout = string.Empty;
        var stderr = string.Empty;

        using var syftTelemetryRecord = new LinuxScannerSyftTelemetryRecord();

        try
        {
            acquired = await DockerSemaphore.WaitAsync(SemaphoreTimeout, cancellationToken);
            if (acquired)
            {
                try
                {
                    var command = new List<string> { imageHash }.Concat(CmdParameters).ToList();
                    (stdout, stderr) = await this.dockerService.CreateAndRunContainerAsync(ScannerImage, command, cancellationToken);
                }
                catch (Exception e)
                {
                    syftTelemetryRecord.Exception = JsonConvert.SerializeObject(e);
                    this.logger.LogError(e, "Failed to run syft");
                    throw;
                }
            }
            else
            {
                record.SemaphoreFailure = true;
                this.logger.LogWarning("Failed to enter the docker semaphore for image {ImageHash}", imageHash);
            }
        }
        finally
        {
            if (acquired)
            {
                DockerSemaphore.Release();
            }
        }

        record.ScanStdErr = stderr;
        record.ScanStdOut = stdout;

        if (string.IsNullOrWhiteSpace(stdout) || !string.IsNullOrWhiteSpace(stderr))
        {
            throw new InvalidOperationException(
                $"Scan failed with exit info: {stdout}{Environment.NewLine}{stderr}");
        }

        var layerDictionary = dockerLayers
            .DistinctBy(layer => layer.DiffId)
            .ToDictionary(
                layer => layer.DiffId,
                _ => new List<LinuxComponent>());

        try
        {
            var syftOutput = JsonConvert.DeserializeObject<SyftOutput>(stdout);
            var linuxComponentsWithLayers = syftOutput.Artifacts
                .DistinctBy(artifact => (artifact.Name, artifact.Version))
                .Where(artifact => AllowedArtifactTypes.Contains(artifact.Type))
                .Select(artifact =>
                    (Component: new LinuxComponent(syftOutput.Distro.Id, syftOutput.Distro.VersionId, artifact.Name, artifact.Version, this.GetLicenseFromArtifactElement(artifact), this.GetSupplierFromArtifactElement(artifact)), layerIds: artifact.Locations.Select(location => location.LayerId).Distinct()));

            foreach (var (component, layers) in linuxComponentsWithLayers)
            {
                layers.ToList().ForEach(layer => layerDictionary[layer].Add(component));
            }

            var layerMappedLinuxComponents = layerDictionary.Select(kvp =>
            {
                (var layerId, var components) = kvp;
                return new LayerMappedLinuxComponents
                {
                    LinuxComponents = components,
                    DockerLayer = dockerLayers.First(layer => layer.DiffId == layerId),
                };
            });

            syftTelemetryRecord.LinuxComponents = JsonConvert.SerializeObject(linuxComponentsWithLayers.Select(linuxComponentWithLayer =>
                new LinuxComponentRecord
                {
                    Name = linuxComponentWithLayer.Component.Name,
                    Version = linuxComponentWithLayer.Component.Version,
                }));

            return layerMappedLinuxComponents;
        }
        catch (Exception e)
        {
            record.FailedDeserializingScannerOutput = e.ToString();
            return null;
        }
    }

    private string GetSupplierFromArtifactElement(ArtifactElement artifact)
    {
        var supplier = artifact.Metadata?.Author;
        if (!string.IsNullOrEmpty(supplier))
        {
            return supplier;
        }

        supplier = artifact.Metadata?.Maintainer;
        if (!string.IsNullOrEmpty(supplier))
        {
            return supplier;
        }

        return null;
    }

    private string GetLicenseFromArtifactElement(ArtifactElement artifact)
    {
        var license = artifact.Metadata?.License?.String;
        if (license != null)
        {
            return license.ToString();
        }

        var licenses = artifact.Licenses;
        if (licenses != null && licenses.Any())
        {
            return string.Join(", ", licenses.Select(l => l.ToString()));
        }

        return null;
    }

    internal sealed class LinuxComponentRecord
    {
        public string Name { get; set; }

        public string Version { get; set; }
    }
}

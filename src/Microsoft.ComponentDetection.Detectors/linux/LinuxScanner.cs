#nullable disable
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
    private const string ScannerImage = "governancecontainerregistry.azurecr.io/syft:v1.16.0@sha256:12774e791a2b2bc48935c73da15180eee7f31815bc978b14f8f85cc408ec960b";

    private static readonly IList<string> CmdParameters =
    [
        "--quiet",
        "--scope",
        "all-layers",
        "--output",
        "json",
    ];

    private static readonly IEnumerable<string> AllowedArtifactTypes = ["apk", "deb", "rpm"];

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

            // This workaround ignores some packages that originate from the mariner 2.0 image that
            // have not been properly tagged with the release and epoch fields in their versions. This
            // was fixed for azurelinux 3.0 with https://github.com/microsoft/azurelinux/pull/10405, but
            // 2.0 no longer recieves non-security updates and will be deprecated in July 2025.
            // This became a problem after the Syft update https://github.com/anchore/syft/pull/3008 which
            // allowed for Syft to respect the package type that is listed in the ELF notes file. Since
            // mariner 2.0 lists the packages as RPMs, Syft categorizes them as such.
            var validArtifacts = syftOutput.Artifacts.ToList();
            if (syftOutput.Distro.Id == "mariner" && syftOutput.Distro.VersionId == "2.0")
            {
                var elfVersionsWithoutRelease = validArtifacts
                    .Where(artifact =>
                        artifact.FoundBy == "elf-binary-package-cataloger" // a number of detectors execute with Syft, this one can container invalid results
                        && !artifact.Version.Contains('-', StringComparison.OrdinalIgnoreCase)) // dash character indicates that the release version was properly appended to the version, so allow these
                    .ToList();

                var elfVersionsRemoved = new List<string>();
                foreach (var elfArtifact in elfVersionsWithoutRelease)
                {
                    elfVersionsRemoved.Add(elfArtifact.Name + " " + elfArtifact.Version);
                    validArtifacts.Remove(elfArtifact);
                }

                syftTelemetryRecord.Mariner2ComponentsRemoved = JsonConvert.SerializeObject(elfVersionsRemoved);
            }

            var linuxComponentsWithLayers = validArtifacts
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
            return license;
        }

        var licenses = artifact.Licenses;
        if (licenses != null && licenses.Length != 0)
        {
            return string.Join(", ", licenses.Select(l => l.Value));
        }

        return null;
    }

    internal sealed class LinuxComponentRecord
    {
        public string Name { get; set; }

        public string Version { get; set; }
    }
}

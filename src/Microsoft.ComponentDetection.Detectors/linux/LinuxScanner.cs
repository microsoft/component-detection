using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using MoreLinq.Extensions;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Detectors.Linux
{
    [Export(typeof(ILinuxScanner))]
    public class LinuxScanner : ILinuxScanner
    {
        private const string ScannerImage = "governancecontainerregistry.azurecr.io/syft:0.49.0@sha256:6c2e6bdffa548140b71db87dba9353099cb58103fcd532ab3d68c495248e5adf";

        private static readonly IList<string> CmdParameters = new List<string>
        {
            "--quiet", "--scope", "all-layers", "--output", "json",
        };

        private static readonly IEnumerable<string> AllowedArtifactTypes = new[] { "apk", "deb", "rpm", "python" };

        private static readonly SemaphoreSlim DockerSemaphore = new SemaphoreSlim(2);

        private static readonly int SemaphoreTimeout = Convert.ToInt32(TimeSpan.FromHours(1).TotalMilliseconds);

        [Import]
        public ILogger Logger { get; set; }

        [Import]
        public IDockerService DockerService { get; set; }

        private static TypedComponent SyftArtifactToComponent(string distroId, string distroVersionId, Package artifact)
        {
            switch (artifact.Type)
            {
                case "apk":
                case "deb":
                case "rpm":
                    return new LinuxComponent(distroId, distroVersionId, artifact.Name, artifact.Version);
                case "python":
                    return new PipComponent(artifact.Name, artifact.Version);
                default:
                    throw new InvalidOperationException(
                        $"Unknown artifact type: `{artifact.Type}`");
            }
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
                        (stdout, stderr) = await DockerService.CreateAndRunContainerAsync(ScannerImage, command, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        syftTelemetryRecord.Exception = JsonConvert.SerializeObject(e);
                        Logger.LogException(e, false);
                        throw;
                    }
                }
                else
                {
                    record.SemaphoreFailure = true;
                    Logger.LogWarning($"Failed to enter the docker semaphore for image {imageHash}");
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
                _ => new List<TypedComponent>());

            try
            {
                var syftOutput = JsonConvert.DeserializeObject<SyftOutput>(stdout);
                var componentsWithLayers = syftOutput.Artifacts
                    .DistinctBy(artifact => (artifact.Name, artifact.Version))
                    .Where(artifact => AllowedArtifactTypes.Contains(artifact.Type))
                    .Select(artifact =>
                        (Component: SyftArtifactToComponent(syftOutput.Distro.Id, syftOutput.Distro.VersionId, artifact), layerIds: artifact.Locations.Select(location => location.LayerId).Distinct()));

                foreach (var (component, layers) in componentsWithLayers)
                {
                    layers.ToList().ForEach(layer => layerDictionary[layer].Add(component));
                }

                var LayerMappedLinuxComponents = layerDictionary.Select(kvp =>
                {
                    (var layerId, var components) = kvp;
                    return new LayerMappedLinuxComponents
                    {
                        Components = components,
                        DockerLayer = dockerLayers.First(layer => layer.DiffId == layerId),
                    };
                });

                syftTelemetryRecord.Components = JsonConvert.SerializeObject(componentsWithLayers.Select(linuxComponentWithLayer =>
                    ComponentToTelemetryRecord(linuxComponentWithLayer.Component)));

                return LayerMappedLinuxComponents;
            }
            catch (Exception e)
            {
                record.FailedDeserializingScannerOutput = e.ToString();
                return null;
            }
        }

        private static Object ComponentToTelemetryRecord(TypedComponent component)
        {
            if (component is LinuxComponent)
            {
                var linuxComponent = (LinuxComponent) component;
                return new
                {
                    Name = linuxComponent.Name,
                    Version = linuxComponent.Version,
                }; 
            }
            else if (component is PipComponent)
            {
                var pipComponent = (PipComponent) component;
                return new
                {
                    Name = pipComponent.Name,
                    Version = pipComponent.Version,
                };
            }
            
            throw new InvalidOperationException(
                $"Unexpected component type: `{component.GetType()}`");
        }
    }
}

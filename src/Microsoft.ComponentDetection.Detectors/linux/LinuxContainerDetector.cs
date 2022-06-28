using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Exceptions;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux.Exceptions;

namespace Microsoft.ComponentDetection.Detectors.Linux
{
    [Export(typeof(IComponentDetector))]
    public class LinuxContainerDetector : IComponentDetector
    {
        [Import]
        public ILogger Logger { get; set; }

        [Import]
        public ILinuxScanner LinuxScanner { get; set; }

        [Import]
        public IDockerService DockerService { get; set; }

        public string Id => "Linux";

        public IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Linux) };

        public IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Linux };

        public int Version => 5;

        public bool NeedsAutomaticRootDependencyCalculation => false;

        public async Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request)
        {
            var imagesToProcess = request.ImagesToScan?.Where(image => !string.IsNullOrWhiteSpace(image))
                .Select(image => image.ToLowerInvariant())
                .ToList();

            if (imagesToProcess == null || !imagesToProcess.Any())
            {
                Logger.LogInfo("No instructions received to scan docker images.");
                return EmptySuccessfulScan();
            }

            var cancellationTokenSource = new CancellationTokenSource(GetTimeout(request.DetectorArgs));

            if (!await DockerService.CanRunLinuxContainersAsync(cancellationTokenSource.Token))
            {
                using var record = new LinuxContainerDetectorUnsupportedOs
                {
                    Os = RuntimeInformation.OSDescription,
                };
                Logger.LogInfo("Linux containers are not available on this host.");
                return EmptySuccessfulScan();
            }

            var results = Enumerable.Empty<ImageScanningResult>();
            try
            {
                results = await ProcessImagesAsync(imagesToProcess, request.ComponentRecorder, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                using var record = new LinuxContainerDetectorTimeout();
            }

            return new IndividualDetectorScanResult
            {
                ContainerDetails = results.Where(tuple => tuple.ContainerDetails != null).Select(tuple => tuple.ContainerDetails).ToList(),
                ResultCode = ProcessingResultCode.Success,
            };
        }

        private async Task<IEnumerable<ImageScanningResult>> ProcessImagesAsync(
            IEnumerable<string> imagesToProcess,
            IComponentRecorder componentRecorder, CancellationToken cancellationToken = default)
        {
            var processedImages = new ConcurrentDictionary<string, ContainerDetails>();

            var inspectTasks = imagesToProcess.Select(
                async image =>
                {
                    try
                    {
                        // Check image exists locally. Try docker pull if not
                        if (!(await DockerService.ImageExistsLocallyAsync(image, cancellationToken) ||
                              await DockerService.TryPullImageAsync(image, cancellationToken)))
                        {
                            throw new InvalidUserInputException(
                                $"Docker image {image} could not be found locally and could not be pulled. Verify the image is either available locally or through docker pull.",
                                null);
                        }

                        var imageDetails = await DockerService.InspectImageAsync(image, cancellationToken);

                        // Unable to fetch image details
                        if (imageDetails == null)
                        {
                            throw new MissingContainerDetailException(image);
                        }

                        processedImages.TryAdd(imageDetails.ImageId, imageDetails);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"Processing of image {image} failed with exception: {e.Message}");
                        using var record = new LinuxContainerDetectorImageDetectionFailed
                        {
                            ExceptionType = e.GetType().ToString(),
                            Message = e.Message,
                            StackTrace = e.StackTrace,
                            ImageId = image,
                        };
                    }
                });

            await Task.WhenAll(inspectTasks);

            var scanTasks = processedImages.Select(async kvp =>
            {
                try
                {
                    var internalContainerDetails = kvp.Value;
                    var image = kvp.Key;
                    int baseImageLayerCount = await GetBaseImageLayerCount(internalContainerDetails, image, cancellationToken);

                    //Update the layer information to specify if a layer was fond in the specified baseImage
                    internalContainerDetails.Layers = internalContainerDetails.Layers.Select(layer => new DockerLayer
                    {
                        DiffId = layer.DiffId,
                        LayerIndex = layer.LayerIndex,
                        IsBaseImage = layer.LayerIndex < baseImageLayerCount,
                    });

                    var layers = await LinuxScanner.ScanLinuxAsync(kvp.Value.ImageId, internalContainerDetails.Layers, baseImageLayerCount, cancellationToken);

                    var components = layers.SelectMany(layer => layer.LinuxComponents.Select(linuxComponent => new DetectedComponent(linuxComponent,  null, internalContainerDetails.Id, layer.DockerLayer.LayerIndex)));
                    internalContainerDetails.Layers = layers.Select(layer => layer.DockerLayer);
                    var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(kvp.Value.ImageId);
                    components.ToList().ForEach(detectedComponent => singleFileComponentRecorder.RegisterUsage(detectedComponent, true));
                    return new ImageScanningResult
                    {
                        ContainerDetails = kvp.Value,
                        Components = components,
                    };
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Scanning of image {kvp.Key} failed with exception: {e.Message}");
                    using var record = new LinuxContainerDetectorImageDetectionFailed
                    {
                        ExceptionType = e.GetType().ToString(),
                        Message = e.Message,
                        StackTrace = e.StackTrace,
                        ImageId = kvp.Value.ImageId,
                    };
                }

                return EmptyImageScanningResult();
            });

            return await Task.WhenAll(scanTasks);
        }

        /// <summary>
        /// Extracts and returns the timeout defined by the user, or a default value if one is not provided.
        /// </summary>
        /// <param name="detectorArgs">The arguments provided by the user.</param>
        /// <returns></returns>
        private static TimeSpan GetTimeout(IDictionary<string, string> detectorArgs)
        {
            if (detectorArgs == null || !detectorArgs.TryGetValue("Linux.ScanningTimeoutSec", out var timeout))
            {
                return TimeSpan.FromMinutes(10);
            }

            return double.TryParse(timeout, out var parsedTimeout) ? TimeSpan.FromSeconds(parsedTimeout) : TimeSpan.FromMinutes(10);
        }

        private static IndividualDetectorScanResult EmptySuccessfulScan()
        {
            return new IndividualDetectorScanResult
            {
                ResultCode = ProcessingResultCode.Success,
            };
        }

        private static ImageScanningResult EmptyImageScanningResult()
        {
            return new ImageScanningResult
            {
                ContainerDetails = null,
                Components = Enumerable.Empty<DetectedComponent>(),
            };
        }

        private async Task<int> GetBaseImageLayerCount(ContainerDetails scannedImageDetails, string image, CancellationToken cancellationToken = default)
        {
            using var record = new LinuxContainerDetectorLayerAwareness
            {
                LayerCount = scannedImageDetails.Layers.Count(),
            };

            if (string.IsNullOrEmpty(scannedImageDetails.BaseImageRef))
            {
                record.BaseImageLayerMessage = $"Base image annotations not found on image {image}, Results will not be mapped to base image layers";
                Logger.LogInfo(record.BaseImageLayerMessage);
                return 0;
            }

            var baseImageDigest = scannedImageDetails.BaseImageDigest;
            var refWithDigest = scannedImageDetails.BaseImageRef + (baseImageDigest != string.Empty ? $"@{baseImageDigest}" : string.Empty);
            record.BaseImageDigest = baseImageDigest;
            record.BaseImageRef = scannedImageDetails.BaseImageRef;

            if (!(await DockerService.ImageExistsLocallyAsync(refWithDigest, cancellationToken) ||
                  await DockerService.TryPullImageAsync(refWithDigest, cancellationToken)))
            {
                record.BaseImageLayerMessage = $"Base image {refWithDigest} could not be found locally and could not be pulled. Results will not be mapped to base image layers";
                Logger.LogInfo(record.BaseImageLayerMessage);
                return 0;
            }

            var baseImageDetails = await DockerService.InspectImageAsync(refWithDigest, cancellationToken);
            if (!ValidateBaseImageLayers(scannedImageDetails, baseImageDetails))
            {
                record.BaseImageLayerMessage = $"Docker image {image} was set to have base image {refWithDigest} but is not built off of it. Results will not be mapped to base image layers";
                Logger.LogInfo(record.BaseImageLayerMessage);
                return 0;
            }

            record.BaseImageLayerCount = baseImageDetails.Layers.Count();
            return baseImageDetails.Layers.Count();
        }

        // Validate that the image actually does start with the layers from the base image specified in the annotations
        private bool ValidateBaseImageLayers(ContainerDetails scannedImageDetails, ContainerDetails baseImageDetails)
        {
            var scannedImageLayers = scannedImageDetails.Layers.ToArray();
            return !(baseImageDetails.Layers.Count() > scannedImageLayers.Length || baseImageDetails.Layers.Where((layer, index) => scannedImageLayers[index].DiffId != layer.DiffId).Any());
        }
    }
}

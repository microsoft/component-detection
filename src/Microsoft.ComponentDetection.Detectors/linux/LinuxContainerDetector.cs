#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;

/// <summary>
/// Detector for Linux container images.
/// </summary>
public class LinuxContainerDetector(
    ILinuxScanner linuxScanner,
    IDockerService dockerService,
    ILogger<LinuxContainerDetector> logger
) : IComponentDetector
{
    private const string TimeoutConfigKey = "Linux.ScanningTimeoutSec";
    private const int DefaultTimeoutMinutes = 10;

    private readonly ILinuxScanner linuxScanner = linuxScanner;
    private readonly IDockerService dockerService = dockerService;
    private readonly ILogger<LinuxContainerDetector> logger = logger;

    /// <inheritdoc/>
    public string Id => "Linux";

    /// <inheritdoc/>
    public IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.Linux)];

    /// <inheritdoc/>
    public IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Linux];

    /// <inheritdoc/>
    public int Version => 8;

    /// <inheritdoc/>
    public bool NeedsAutomaticRootDependencyCalculation => false;

    /// <summary>
    /// Gets the component types that should be detected by this detector.
    /// By default, only Linux system packages are detected.
    /// Override this method in derived classes to enable detection of additional component types.
    /// </summary>
    /// <returns>A set of component types to include in scan results.</returns>
    protected virtual ISet<ComponentType> GetEnabledComponentTypes() =>
        new HashSet<ComponentType> { ComponentType.Linux };

    /// <inheritdoc/>
    public async Task<IndividualDetectorScanResult> ExecuteDetectorAsync(
        ScanRequest request,
        CancellationToken cancellationToken = default
    )
    {
#pragma warning disable CA1308
        var imagesToProcess = request
            .ImagesToScan?.Where(image => !string.IsNullOrWhiteSpace(image))
            .Select(image => image.ToLowerInvariant())
            .ToList();
#pragma warning restore CA1308

        if (imagesToProcess == null || imagesToProcess.Count == 0)
        {
            this.logger.LogInformation("No instructions received to scan container images.");
            return EmptySuccessfulScan();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GetTimeout(request.DetectorArgs));

        if (!await this.dockerService.CanRunLinuxContainersAsync(timeoutCts.Token))
        {
            using var record = new LinuxContainerDetectorUnsupportedOs
            {
                Os = RuntimeInformation.OSDescription,
            };
            this.logger.LogInformation("Linux containers are not available on this host.");
            return EmptySuccessfulScan();
        }

        var results = Enumerable.Empty<ImageScanningResult>();
        try
        {
            results = await this.ProcessImagesAsync(
                imagesToProcess,
                request.ComponentRecorder,
                timeoutCts.Token
            );
        }
        catch (OperationCanceledException)
        {
            using var record = new LinuxContainerDetectorTimeout();
            this.logger.LogWarning(
                "Container image scanning timed out after {Timeout}",
                GetTimeout(request.DetectorArgs)
            );
        }

        return new IndividualDetectorScanResult
        {
            ContainerDetails = results
                .Where(tuple => tuple.ContainerDetails != null)
                .Select(tuple => tuple.ContainerDetails)
                .ToList(),
            ResultCode = ProcessingResultCode.Success,
        };
    }

    /// <summary>
    /// Extracts and returns the timeout defined by the user, or a default value if one is not provided.
    /// </summary>
    /// <param name="detectorArgs">The arguments provided by the user.</param>
    /// <returns> Time interval <see cref="TimeSpan"/> representing the timeout defined by the user, or a default value if one is not provided. </returns>
    private static TimeSpan GetTimeout(IDictionary<string, string> detectorArgs)
    {
        var defaultTimeout = TimeSpan.FromMinutes(DefaultTimeoutMinutes);

        if (detectorArgs == null || !detectorArgs.TryGetValue(TimeoutConfigKey, out var timeout))
        {
            return defaultTimeout;
        }

        return double.TryParse(timeout, out var parsedTimeout)
            ? TimeSpan.FromSeconds(parsedTimeout)
            : defaultTimeout;
    }

    private static IndividualDetectorScanResult EmptySuccessfulScan() =>
        new() { ResultCode = ProcessingResultCode.Success };

    /// <summary>
    /// Creates an empty <see cref="ImageScanningResult"/> instance with no container details or components.
    /// Used when image processing fails.
    /// </summary>
    /// <returns>An <see cref="ImageScanningResult"/> with null container details and an empty components collection.</returns>
    private static ImageScanningResult EmptyImageScanningResult() =>
        new() { ContainerDetails = null, Components = [] };

    /// <summary>
    /// Validate that the image actually does start with the layers from the base image specified in the annotations.
    /// </summary>
    private static bool ValidateBaseImageLayers(
        ContainerDetails scannedImageDetails,
        ContainerDetails baseImageDetails
    )
    {
        var scannedImageLayers = scannedImageDetails.Layers.ToArray();
        return !(
            baseImageDetails.Layers.Count() > scannedImageLayers.Length
            || baseImageDetails
                .Layers.Where((layer, index) => scannedImageLayers[index].DiffId != layer.DiffId)
                .Any()
        );
    }

    private static void RecordImageDetectionFailure(Exception exception, string imageId)
    {
        using var record = new LinuxContainerDetectorImageDetectionFailed
        {
            ExceptionType = exception.GetType().ToString(),
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            ImageId = imageId,
        };
    }

    private async Task<IEnumerable<ImageScanningResult>> ProcessImagesAsync(
        IEnumerable<string> imagesToProcess,
        IComponentRecorder componentRecorder,
        CancellationToken cancellationToken = default
    )
    {
        var processedImages = new ConcurrentDictionary<string, ContainerDetails>();

        var inspectTasks = imagesToProcess.Select(async image =>
        {
            try
            {
                // Check image exists locally. Try pulling if not
                if (
                    !(
                        await this.dockerService.ImageExistsLocallyAsync(image, cancellationToken)
                        || await this.dockerService.TryPullImageAsync(image, cancellationToken)
                    )
                )
                {
                    throw new InvalidUserInputException(
                        $"Container image {image} could not be found locally and could not be pulled. Verify the image is either available locally or can be pulled from a registry.",
                        null
                    );
                }

                var imageDetails =
                    await this.dockerService.InspectImageAsync(image, cancellationToken)
                    ?? throw new MissingContainerDetailException(image);

                processedImages.TryAdd(imageDetails.ImageId, imageDetails);
            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "Processing of image {ContainerImage} failed", image);
                RecordImageDetectionFailure(e, image);

                var singleFileComponentRecorder =
                    componentRecorder.CreateSingleFileComponentRecorder(image);
                singleFileComponentRecorder.RegisterPackageParseFailure(image);
            }
        });

        await Task.WhenAll(inspectTasks);

        var scanTasks = processedImages.Select(async kvp =>
        {
            try
            {
                var internalContainerDetails = kvp.Value;
                var image = kvp.Key;
                var baseImageLayerCount = await this.GetBaseImageLayerCountAsync(
                    internalContainerDetails,
                    image,
                    cancellationToken
                );

                // Update the layer information to specify if a layer was found in the specified baseImage
                internalContainerDetails.Layers = internalContainerDetails.Layers.Select(
                    layer => new DockerLayer
                    {
                        DiffId = layer.DiffId,
                        LayerIndex = layer.LayerIndex,
                        IsBaseImage = layer.LayerIndex < baseImageLayerCount,
                    }
                );

                var enabledComponentTypes = this.GetEnabledComponentTypes();
                var layers = await this.linuxScanner.ScanLinuxAsync(
                    kvp.Value.ImageId,
                    internalContainerDetails.Layers,
                    baseImageLayerCount,
                    enabledComponentTypes,
                    cancellationToken
                );

                var components = layers.SelectMany(layer =>
                    layer.Components.Select(component => new DetectedComponent(
                        component,
                        null,
                        internalContainerDetails.Id,
                        layer.DockerLayer.LayerIndex
                    ))
                );
                internalContainerDetails.Layers = layers.Select(layer => layer.DockerLayer);
                var singleFileComponentRecorder =
                    componentRecorder.CreateSingleFileComponentRecorder(kvp.Value.ImageId);
                components
                    .ToList()
                    .ForEach(detectedComponent =>
                        singleFileComponentRecorder.RegisterUsage(detectedComponent, true)
                    );
                return new ImageScanningResult
                {
                    ContainerDetails = kvp.Value,
                    Components = components,
                };
            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "Scanning of image {ImageId} failed", kvp.Value.ImageId);
                RecordImageDetectionFailure(e, kvp.Value.ImageId);

                var singleFileComponentRecorder =
                    componentRecorder.CreateSingleFileComponentRecorder(kvp.Value.ImageId);
                singleFileComponentRecorder.RegisterPackageParseFailure(kvp.Key);
            }

            return EmptyImageScanningResult();
        });

        return await Task.WhenAll(scanTasks);
    }

    private async Task<int> GetBaseImageLayerCountAsync(
        ContainerDetails scannedImageDetails,
        string image,
        CancellationToken cancellationToken = default
    )
    {
        var layerCount = scannedImageDetails.Layers.Count();
        using var record = new LinuxContainerDetectorLayerAwareness { LayerCount = layerCount };

        if (string.IsNullOrEmpty(scannedImageDetails.BaseImageRef))
        {
            record.BaseImageLayerMessage =
                "Base image annotations not found, results will not be mapped to base image layers";
            this.logger.LogInformation(
                "Base image annotations not found on image {ContainerImage}, results will not be mapped to base image layers",
                image
            );
            return 0;
        }

        if (scannedImageDetails.BaseImageRef == "scratch")
        {
            record.BaseImageLayerMessage = "Image has no base image";
            this.logger.LogInformation("{ContainerImage} has no base image", image);
            return 0;
        }

        var baseImageDigest = scannedImageDetails.BaseImageDigest;
        var refWithDigest =
            scannedImageDetails.BaseImageRef
            + (!string.IsNullOrEmpty(baseImageDigest) ? $"@{baseImageDigest}" : string.Empty);
        record.BaseImageDigest = baseImageDigest;
        record.BaseImageRef = scannedImageDetails.BaseImageRef;

        if (
            !(
                await this.dockerService.ImageExistsLocallyAsync(refWithDigest, cancellationToken)
                || await this.dockerService.TryPullImageAsync(refWithDigest, cancellationToken)
            )
        )
        {
            record.BaseImageLayerMessage =
                "Base image could not be found locally and could not be pulled";
            this.logger.LogInformation(
                "Base image {BaseImage} could not be found locally and could not be pulled. Results will not be mapped to base image layers",
                refWithDigest
            );
            return 0;
        }

        var baseImageDetails = await this.dockerService.InspectImageAsync(
            refWithDigest,
            cancellationToken
        );
        if (!ValidateBaseImageLayers(scannedImageDetails, baseImageDetails))
        {
            record.BaseImageLayerMessage =
                "Image was set to have a base image but is not built off of it";
            this.logger.LogInformation(
                "Container image {ContainerImage} was set to have base image {BaseImage} but is not built off of it. Results will not be mapped to base image layers",
                image,
                refWithDigest
            );
            return 0;
        }

        var baseImageLayerCount = baseImageDetails.Layers.Count();
        record.BaseImageLayerCount = baseImageLayerCount;
        return baseImageLayerCount;
    }
}

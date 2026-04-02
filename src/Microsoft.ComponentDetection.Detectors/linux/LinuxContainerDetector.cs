namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    private const string ScanScopeConfigKey = "Linux.ImageScanScope";
    private const LinuxScannerScope DefaultScanScope = LinuxScannerScope.AllLayers;

    private const string LocalImageMountPoint = "/image";

    // Base image annotations from ADO dockerTask
    private const string BaseImageRefAnnotation = "image.base.ref.name";
    private const string BaseImageDigestAnnotation = "image.base.digest";

    private readonly ILinuxScanner linuxScanner = linuxScanner;
    private readonly IDockerService dockerService = dockerService;
    private readonly ILogger<LinuxContainerDetector> logger = logger;

    /// <inheritdoc/>
    public string Id => "Linux";

    /// <inheritdoc/>
    public IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.Linux)!];

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
        var allImages = request
            .ImagesToScan?.Where(image => !string.IsNullOrWhiteSpace(image))
            .Select(ImageReference.Parse)
            .ToList();

        if (allImages == null || allImages.Count == 0)
        {
            this.logger.LogInformation("No instructions received to scan container images.");
            return EmptySuccessfulScan();
        }

        var scannerScope = GetScanScope(request.DetectorArgs);

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
                allImages,
                request.ComponentRecorder,
                scannerScope,
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

    /// <summary>
    /// Extracts and returns the scan scope from detector arguments.
    /// </summary>
    /// <param name="detectorArgs">The arguments provided by the user.</param>
    /// <returns>The <see cref="LinuxScannerScope"/> to use for scanning. Defaults to <see cref="DefaultScanScope"/> if not specified.</returns>
    private static LinuxScannerScope GetScanScope(IDictionary<string, string> detectorArgs)
    {
        if (
            detectorArgs == null
            || !detectorArgs.TryGetValue(ScanScopeConfigKey, out var scopeValue)
        )
        {
            return DefaultScanScope;
        }

        return scopeValue?.ToUpperInvariant() switch
        {
            "ALL-LAYERS" => LinuxScannerScope.AllLayers,
            "SQUASHED" => LinuxScannerScope.Squashed,
            _ => DefaultScanScope,
        };
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
        IEnumerable<ImageReference> imageReferences,
        IComponentRecorder componentRecorder,
        LinuxScannerScope scannerScope,
        CancellationToken cancellationToken = default
    )
    {
        // Phase 1: Resolve images.

        // Docker images will resolve to ContainerDetails via inspect. Deduplicate by ImageId since multiple refs can resolve to the same image.
        var processedDockerImages = new ConcurrentDictionary<string, ContainerDetails>();

        // Local images will be validated for existence and tracked by their file path.
        var localImages = new ConcurrentDictionary<string, ImageReferenceKind>();

        var resolveTasks = imageReferences.Select(imageRef =>
            this.ResolveImageAsync(imageRef, processedDockerImages, localImages, componentRecorder, cancellationToken));

        await Task.WhenAll(resolveTasks);

        // Phase 2: Scan and record components for all resolved images concurrently.
        var scanTasks = new List<Task<ImageScanningResult>>();

        scanTasks.AddRange(processedDockerImages.Select(kvp =>
            this.ScanDockerImageAsync(kvp.Key, kvp.Value, scannerScope, componentRecorder, cancellationToken)));

        scanTasks.AddRange(localImages
            .Select(kvp =>
                this.ScanLocalImageAsync(kvp.Key, kvp.Value, scannerScope, componentRecorder, cancellationToken)));

        return await Task.WhenAll(scanTasks);
    }

    /// <summary>
    /// Resolves an image by doing one of the following:
    /// * For Docker images, resolve the reference by pulling (if needed) and inspecting it.
    ///   Adds the result to the processedImages dictionary for deduplication.
    /// * For local images, verify the path exists and adds the reference to a concurrent
    ///   set for tracking which images to scan in phase 2.
    /// </summary>
    private async Task ResolveImageAsync(
        ImageReference imageRef,
        ConcurrentDictionary<string, ContainerDetails> resolvedDockerImages,
        ConcurrentDictionary<string, ImageReferenceKind> localImages,
        IComponentRecorder componentRecorder,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (imageRef.Kind)
            {
                case ImageReferenceKind.DockerImage:
                    await this.ResolveDockerImageAsync(imageRef.Reference, resolvedDockerImages, cancellationToken);
                    break;
                case ImageReferenceKind.OciLayout:
                case ImageReferenceKind.OciArchive:
                case ImageReferenceKind.DockerArchive:
                    var fullPath = this.ValidateLocalImagePath(imageRef);
                    localImages.TryAdd(fullPath, imageRef.Kind);
                    break;
                default:
                    throw new InvalidUserInputException(
                        $"Unsupported image reference kind '{imageRef.Kind}' for image '{imageRef.OriginalInput}'."
                    );
            }
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Processing of image {ContainerImage} (kind {ImageType}) failed", imageRef.OriginalInput, imageRef.Kind);
            RecordImageDetectionFailure(e, imageRef.OriginalInput);

            var singleFileComponentRecorder =
                componentRecorder.CreateSingleFileComponentRecorder(imageRef.OriginalInput);
            singleFileComponentRecorder.RegisterPackageParseFailure(imageRef.OriginalInput);
        }
    }

    private async Task ResolveDockerImageAsync(
        string image,
        ConcurrentDictionary<string, ContainerDetails> resolvedDockerImages,
        CancellationToken cancellationToken)
    {
        if (
            !(
                await this.dockerService.ImageExistsLocallyAsync(image, cancellationToken)
                || await this.dockerService.TryPullImageAsync(image, cancellationToken)
            )
        )
        {
            throw new InvalidUserInputException(
                $"Container image {image} could not be found locally and could not be pulled. Verify the image is either available locally or can be pulled from a registry."
            );
        }

        var imageDetails =
            await this.dockerService.InspectImageAsync(image, cancellationToken)
            ?? throw new MissingContainerDetailException(image);

        resolvedDockerImages.TryAdd(imageDetails.ImageId, imageDetails);
    }

    /// <summary>
    /// Validates that a local image path exists on disk. Throws a <see cref="FileNotFoundException"/> if it does not.
    /// For OCI layouts, checks for a directory. For OCI archives and Docker archives, checks for a file.
    /// Returns the full path to the local image if validation succeeds.
    /// </summary>
    private string ValidateLocalImagePath(ImageReference imageRef)
    {
        var path = Path.GetFullPath(imageRef.Reference);
        var exists = imageRef.Kind switch
        {
            ImageReferenceKind.OciLayout => Directory.Exists(path),
            ImageReferenceKind.OciArchive => System.IO.File.Exists(path),
            ImageReferenceKind.DockerArchive => System.IO.File.Exists(path),
            ImageReferenceKind.DockerImage or _ => throw new InvalidOperationException(
                $"ValidateLocalImagePath does not support image kind '{imageRef.Kind}'."),
        };

        if (!exists)
        {
            throw new FileNotFoundException(
                $"Local image at path {imageRef.Reference} does not exist.",
                imageRef.Reference
            );
        }

        return path;
    }

    /// <summary>
    /// Scans a Docker image (already inspected) and records its components.
    /// </summary>
    private async Task<ImageScanningResult> ScanDockerImageAsync(
        string imageId,
        ContainerDetails containerDetails,
        LinuxScannerScope scannerScope,
        IComponentRecorder componentRecorder,
        CancellationToken cancellationToken)
    {
        try
        {
            var baseImageLayerCount = await this.GetBaseImageLayerCountAsync(
                containerDetails,
                imageId,
                cancellationToken
            );

            // Update layers with base image attribution
            containerDetails.Layers = containerDetails.Layers.Select(
                layer => new DockerLayer
                {
                    DiffId = layer.DiffId,
                    LayerIndex = layer.LayerIndex,
                    IsBaseImage = layer.LayerIndex < baseImageLayerCount,
                }
            ).ToList();

            var enabledComponentTypes = this.GetEnabledComponentTypes();
            var layers = await this.linuxScanner.ScanLinuxAsync(
                containerDetails.ImageId,
                containerDetails.Layers,
                baseImageLayerCount,
                enabledComponentTypes,
                scannerScope,
                cancellationToken
            ) ?? throw new InvalidOperationException($"Failed to scan image layers for image {containerDetails.ImageId}");

            return this.RecordComponents(containerDetails, layers, componentRecorder);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "Scanning of image {ImageId} failed", containerDetails.ImageId);
            RecordImageDetectionFailure(e, containerDetails.ImageId);

            var singleFileComponentRecorder =
                componentRecorder.CreateSingleFileComponentRecorder(containerDetails.ImageId);
            singleFileComponentRecorder.RegisterPackageParseFailure(imageId);
        }

        return EmptyImageScanningResult();
    }

    /// <summary>
    /// Scans a local image (OCI layout directory or archive file) by invoking Syft with a volume
    /// mount, extracting metadata from the Syft output to build ContainerDetails, and processing
    /// detected components.
    /// </summary>
    private async Task<ImageScanningResult> ScanLocalImageAsync(
        string localImagePath,
        ImageReferenceKind imageRefKind,
        LinuxScannerScope scannerScope,
        IComponentRecorder componentRecorder,
        CancellationToken cancellationToken)
    {
        string hostPathToBind;
        string syftContainerPath;
        switch (imageRefKind)
        {
            case ImageReferenceKind.OciLayout:
                hostPathToBind = localImagePath;
                syftContainerPath = $"oci-dir:{LocalImageMountPoint}";
                break;
            case ImageReferenceKind.OciArchive:
                hostPathToBind = Path.GetDirectoryName(localImagePath)
                    ?? throw new InvalidOperationException($"Could not determine parent directory for OCI archive path '{localImagePath}'.");
                syftContainerPath = $"oci-archive:{LocalImageMountPoint}/{Path.GetFileName(localImagePath)}";
                break;
            case ImageReferenceKind.DockerArchive:
                hostPathToBind = Path.GetDirectoryName(localImagePath)
                    ?? throw new InvalidOperationException($"Could not determine parent directory for Docker archive path '{localImagePath}'.");
                syftContainerPath = $"docker-archive:{LocalImageMountPoint}/{Path.GetFileName(localImagePath)}";
                break;
            case ImageReferenceKind.DockerImage:
            default:
                throw new InvalidUserInputException(
                    $"Unsupported image reference kind '{imageRefKind}' for local image at path '{localImagePath}'."
                );
        }

        try
        {
            var additionalBinds = new List<string>
            {
                // Bind the local image path into the Syft container as read-only
                $"{hostPathToBind}:{LocalImageMountPoint}:ro",
            };

            var syftOutput = await this.linuxScanner.GetSyftOutputAsync(
                syftContainerPath,
                additionalBinds,
                scannerScope,
                cancellationToken
            );

            SyftSourceMetadata? sourceMetadata = null;
            try
            {
                sourceMetadata = syftOutput.Source?.GetSyftSourceMetadata();
            }
            catch (Exception e)
            {
                this.logger.LogWarning(
                    e,
                    "Failed to deserialize Syft source metadata for local image at {LocalImagePath}. Proceeding without metadata",
                    localImagePath
                );
            }

            if (sourceMetadata?.Layers == null || sourceMetadata.Layers.Length == 0)
            {
                this.logger.LogWarning(
                    "No layer information found in Syft output for local image at {LocalImagePath}",
                    localImagePath
                );
            }

            // Build ContainerDetails from Syft source metadata
            var containerDetails = this.dockerService.GetEmptyContainerDetails();
            containerDetails.ImageId = !string.IsNullOrWhiteSpace(sourceMetadata?.ImageId)
                ? sourceMetadata.ImageId
                : localImagePath;
            containerDetails.Digests = sourceMetadata?.RepoDigests ?? [];
            containerDetails.Tags = sourceMetadata?.Tags ?? [];
            containerDetails.Layers = sourceMetadata?.Layers?
                .Select((layer, index) => new DockerLayer
                {
                    DiffId = layer.Digest ?? string.Empty,
                    LayerIndex = index,
                })
                .ToList() ?? [];

            // Extract base image annotations from the Syft source metadata labels
            var baseImageRef = string.Empty;
            var baseImageDigest = string.Empty;
            sourceMetadata?.Labels?.TryGetValue(BaseImageRefAnnotation, out baseImageRef);
            sourceMetadata?.Labels?.TryGetValue(BaseImageDigestAnnotation, out baseImageDigest);
            containerDetails.BaseImageRef = baseImageRef;
            containerDetails.BaseImageDigest = baseImageDigest;

            // Determine base image layer count using existing logic
            var baseImageLayerCount = await this.GetBaseImageLayerCountAsync(
                containerDetails,
                localImagePath,
                cancellationToken
            );

            // Update layers with base image attribution
            containerDetails.Layers = containerDetails.Layers.Select(
                layer => new DockerLayer
                {
                    DiffId = layer.DiffId,
                    LayerIndex = layer.LayerIndex,
                    IsBaseImage = layer.LayerIndex < baseImageLayerCount,
                }
            ).ToList();

            // Process components from the same Syft output
            var enabledComponentTypes = this.GetEnabledComponentTypes();
            var layers = this.linuxScanner.ProcessSyftOutput(
                syftOutput,
                containerDetails.Layers,
                enabledComponentTypes
            );

            return this.RecordComponents(containerDetails, layers, componentRecorder);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(
                e,
                "Processing of local image at {LocalImagePath} failed",
                localImagePath
            );
            RecordImageDetectionFailure(e, localImagePath);

            var singleFileComponentRecorder =
                componentRecorder.CreateSingleFileComponentRecorder(localImagePath);
            singleFileComponentRecorder.RegisterPackageParseFailure(localImagePath);
        }

        return EmptyImageScanningResult();
    }

    /// <summary>
    /// Records detected components from layer-mapped scan results into the component recorder.
    /// </summary>
    private ImageScanningResult RecordComponents(
        ContainerDetails containerDetails,
        IEnumerable<LayerMappedLinuxComponents> layers,
        IComponentRecorder componentRecorder)
    {
        var materializedLayers = layers.ToList();
        var components = materializedLayers.SelectMany(layer =>
            layer.Components.Select(component => new DetectedComponent(
                component,
                null,
                containerDetails.Id,
                layer.DockerLayer.LayerIndex
            ))
        ).ToList();
        containerDetails.Layers = materializedLayers.Select(layer => layer.DockerLayer);

        var singleFileComponentRecorder =
            componentRecorder.CreateSingleFileComponentRecorder(containerDetails.ImageId);
        components.ForEach(detectedComponent =>
            singleFileComponentRecorder.RegisterUsage(detectedComponent, true)
        );

        return new ImageScanningResult
        {
            ContainerDetails = containerDetails,
            Components = components,
        };
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

namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LinuxContainerDetectorTests
{
    private const string NodeLatestImage = "node:latest";
    private const string NodeLatestDigest = "2a22e4a1a550";
    private const string BashPackageId = "Ubuntu 20.04 bash 5.0-6ubuntu1 - Linux";

    private static readonly IEnumerable<LayerMappedLinuxComponents> LinuxComponents =
    [
        new LayerMappedLinuxComponents
        {
            DockerLayer = new DockerLayer(),
            Components = [new LinuxComponent("Ubuntu", "20.04", "bash", "5.0-6ubuntu1")],
        },
    ];

    private readonly Mock<IDockerService> mockDockerService;
    private readonly Mock<ILogger> mockLogger;
    private readonly Mock<ILogger<LinuxContainerDetector>> mockLinuxContainerDetectorLogger;
    private readonly Mock<IDockerSyftRunner> mockDockerSyftRunner;
    private readonly Mock<IBinarySyftRunnerFactory> mockBinarySyftRunnerFactory;
    private readonly Mock<ILinuxScanner> mockSyftLinuxScanner;

    public LinuxContainerDetectorTests()
    {
        this.mockDockerService = new Mock<IDockerService>();
        this.mockDockerService.Setup(service =>
                service.TryPullImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        this.mockDockerService.Setup(service =>
                service.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new ContainerDetails
                {
                    Id = 1,
                    ImageId = NodeLatestDigest,
                    Layers = [],
                }
            );
        this.mockDockerService.Setup(service => service.GetEmptyContainerDetails())
            .Returns(() => new ContainerDetails { Id = 100 });

        this.mockLogger = new Mock<ILogger>();
        this.mockLinuxContainerDetectorLogger = new Mock<ILogger<LinuxContainerDetector>>();
        this.mockDockerSyftRunner = new Mock<IDockerSyftRunner>();
        this.mockDockerSyftRunner.Setup(runner =>
                runner.CanRunAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        this.mockBinarySyftRunnerFactory = new Mock<IBinarySyftRunnerFactory>();

        this.mockSyftLinuxScanner = new Mock<ILinuxScanner>();
        this.mockSyftLinuxScanner.Setup(scanner =>
                scanner.ScanLinuxAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IEnumerable<DockerLayer>>(),
                    It.IsAny<int>(),
                    It.IsAny<ISet<ComponentType>>(),
                    It.IsAny<LinuxScannerScope>(),
                    It.IsAny<ISyftRunner>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(LinuxComponents);
    }

    [TestMethod]
    public async Task TestLinuxContainerDetectorAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            null,
            [NodeLatestImage],
            componentRecorder
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);
        scanResult.ContainerDetails.Should().ContainSingle();
        detectedComponents
            .Should()
            .OnlyContain(dc =>
                dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id)
            );
        componentRecorder
            .GetDetectedComponents()
            .Select(detectedComponent => detectedComponent.Component.Id)
            .Should()
            .BeEquivalentTo(
                detectedComponents.Select(detectedComponent => detectedComponent.Component.Id)
            );
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_CantRunLinuxContainersAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            null,
            [NodeLatestImage],
            componentRecorder
        );

        this.mockDockerSyftRunner.Setup(runner =>
                runner.CanRunAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().BeEmpty();
        scanResult.ContainerDetails.Should().BeEmpty();
        this.mockLinuxContainerDetectorLogger.Verify(logger =>
            logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
            )
        );
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_TestNullAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            null,
            null,
            componentRecorder
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().BeEmpty();
        scanResult.ContainerDetails.Should().BeEmpty();
        this.mockLinuxContainerDetectorLogger.Verify(logger =>
            logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
            )
        );
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_VerifyLowerCaseAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            null,
            ["UPPERCASE"],
            componentRecorder
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);
        scanResult.ContainerDetails.Should().ContainSingle();
        detectedComponents
            .Should()
            .OnlyContain(dc =>
                dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id)
            );
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_SameImagePassedMultipleTimesAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            null,
            [NodeLatestImage, NodeLatestDigest],
            componentRecorder
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        scanResult.ContainerDetails.Should().ContainSingle();
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);
        detectedComponents
            .Should()
            .OnlyContain(dc =>
                dc.ContainerDetailIds.Contains(scanResult.ContainerDetails.First().Id)
            );
        this.mockSyftLinuxScanner.Verify(
            scanner =>
                scanner.ScanLinuxAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IEnumerable<DockerLayer>>(),
                    It.IsAny<int>(),
                    It.IsAny<ISet<ComponentType>>(),
                    It.IsAny<LinuxScannerScope>(),
                    It.IsAny<ISyftRunner>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_TimeoutParameterSpecifiedAsync()
    {
        var detectorArgs = new Dictionary<string, string> { { "Linux.ScanningTimeoutSec", "2" } };
        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            detectorArgs,
            [NodeLatestImage],
            new ComponentRecorder()
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        Func<Task> action = async () =>
            await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);
        await action.Should().NotThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    [DataRow("all-layers", LinuxScannerScope.AllLayers)]
    [DataRow("squashed", LinuxScannerScope.Squashed)]
    [DataRow("ALL-LAYERS", LinuxScannerScope.AllLayers)]
    [DataRow("SQUASHED", LinuxScannerScope.Squashed)]
    [DataRow(null, LinuxScannerScope.AllLayers)] // Test default behavior
    [DataRow("", LinuxScannerScope.AllLayers)] // Test empty string default
    [DataRow("invalid-value", LinuxScannerScope.AllLayers)] // Test invalid input defaults to AllLayers
    public async Task TestLinuxContainerDetector_ImageScanScopeParameterSpecifiedAsync(string scopeValue, LinuxScannerScope expectedScope)
    {
        var detectorArgs = new Dictionary<string, string> { { "Linux.ImageScanScope", scopeValue } };
        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            detectorArgs,
            [NodeLatestImage],
            new ComponentRecorder()
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        this.mockSyftLinuxScanner.Verify(
            scanner =>
                scanner.ScanLinuxAsync(
                    It.IsAny<ImageReference>(),
                    It.IsAny<IEnumerable<DockerLayer>>(),
                    It.IsAny<int>(),
                    It.IsAny<ISet<ComponentType>>(),
                    expectedScope,
                    It.IsAny<ISyftRunner>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_HandlesScratchBaseAsync()
    {
        // Setup docker service to throw an exception on scratch
        // then specify that the base image is scratch, to test this
        // is coped with.
        this.mockDockerService.Setup(service =>
                service.TryPullImageAsync("scratch", It.IsAny<CancellationToken>())
            )
            .Throws(new IOException());
        this.mockDockerService.Setup(service =>
                service.InspectImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new ContainerDetails
                {
                    Id = 1,
                    ImageId = NodeLatestDigest,
                    Layers = [],
                    BaseImageRef = "scratch",
                }
            );
        await this.TestLinuxContainerDetectorAsync();
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_OciLayoutImage_DetectsComponentsAsync()
    {
        var componentRecorder = new ComponentRecorder();

        // Create a temp directory to act as the OCI layout path
        var ociDir = Path.Combine(Path.GetTempPath(), "test-oci-layout-" + Guid.NewGuid().ToString("N")).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(ociDir);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"oci-dir:{ociDir}"],
                componentRecorder
            );

            // Build a SyftOutput with source metadata containing layers, labels, tags
            var syftOutputJson = """
                {
                    "distro": { "id": "azurelinux", "versionID": "3.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "userInput": "/oci-image",
                            "imageID": "sha256:ociimage123",
                            "tags": ["myregistry.io/myimage:latest"],
                            "repoDigests": [],
                            "layers": [
                                { "digest": "sha256:layer1", "size": 40000 },
                                { "digest": "sha256:layer2", "size": 50000 }
                            ],
                            "labels": {
                                "image.base.ref.name": "mcr.microsoft.com/azurelinux/base/core:3.0",
                                "image.base.digest": "sha256:basedigest"
                            }
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            var layerMappedComponents = new[]
            {
                new LayerMappedLinuxComponents
                {
                    DockerLayer = new DockerLayer { DiffId = "sha256:layer1", LayerIndex = 0 },
                    Components = [new LinuxComponent("azurelinux", "3.0", "bash", "5.2.15")],
                },
            };

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns(layerMappedComponents);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            scanResult.ContainerDetails.Should().ContainSingle();

            var containerDetails = scanResult.ContainerDetails.First();
            containerDetails.ImageId.Should().Be("sha256:ociimage123");
            containerDetails.BaseImageRef.Should().Be("mcr.microsoft.com/azurelinux/base/core:3.0");
            containerDetails.BaseImageDigest.Should().Be("sha256:basedigest");
            containerDetails.Tags.Should().ContainSingle().Which.Should().Be("myregistry.io/myimage:latest");

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
            detectedComponents.Should().ContainSingle();
            var detectedComponent = detectedComponents.First();
            detectedComponent.Component.Id.Should().Contain("bash");
            detectedComponent.ContainerLayerIds.Keys.Should().ContainSingle();
            var containerId = detectedComponent.ContainerLayerIds.Keys.First();
            detectedComponent.ContainerLayerIds[containerId].Should().BeEquivalentTo([0]); // Layer index from SyftOutput

            // Verify GetSyftOutputAsync was called (not ScanLinuxAsync)
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.GetSyftOutputAsync(
                        It.Is<ImageReference>(r => r.Kind == ImageReferenceKind.OciLayout),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

            // Verify Docker inspect was NOT called for OCI images
            this.mockDockerService.Verify(
                service =>
                    service.InspectImageAsync(ociDir, It.IsAny<CancellationToken>()),
                Times.Never
            );

            // Verify ProcessSyftOutput was called with the correct layers
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.Is<IEnumerable<DockerLayer>>(layers =>
                            layers.Count() == 2 &&
                            layers.First().DiffId == "sha256:layer1" &&
                            layers.Last().DiffId == "sha256:layer2"
                        ),
                        It.IsAny<ISet<ComponentType>>()
                    ),
                Times.Once
            );
        }
        finally
        {
            Directory.Delete(ociDir, true);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_OciLayoutImage_DoesNotLowercasePathAsync()
    {
        var componentRecorder = new ComponentRecorder();

        // Create a temp directory with mixed case
        var ociDir = Path.Combine(Path.GetTempPath(), "TestOciLayout-" + Guid.NewGuid().ToString("N")).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(ociDir);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"oci-dir:{ociDir}"],
                componentRecorder
            );

            var syftOutputJson = """
                {
                    "distro": { "id": "test", "versionID": "1.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "userInput": "/oci-image",
                            "imageID": "sha256:img",
                            "layers": [],
                            "labels": {}
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns([]);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            // Verify the bind mount path was passed as-is (not lowercased)
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.GetSyftOutputAsync(
                        It.Is<ImageReference>(r => r.Kind == ImageReferenceKind.OciLayout && r.Reference == ociDir),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
        finally
        {
            Directory.Delete(ociDir, true);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_OciLayoutImage_NormalizesPathAsync()
    {
        var componentRecorder = new ComponentRecorder();

        // Create a temp directory with mixed case
        var ociDir = Path.Combine(Path.GetTempPath(), "test-oci-layout-" + Guid.NewGuid().ToString("N")).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(ociDir);

        var ociDirWithExtraComponents = Path.Combine(Path.GetDirectoryName(ociDir)!, ".", "random", "..", Path.GetFileName(ociDir));

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"oci-dir:{ociDirWithExtraComponents}"],
                componentRecorder
            );

            var syftOutputJson = """
                {
                    "distro": { "id": "test", "versionID": "1.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "userInput": "/oci-image",
                            "imageID": "sha256:img",
                            "layers": [],
                            "labels": {}
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns([]);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.GetSyftOutputAsync(
                        It.Is<ImageReference>(r => r.Kind == ImageReferenceKind.OciLayout && r.Reference.Contains(ociDir) && !r.Reference.Contains(ociDirWithExtraComponents)),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
        finally
        {
            Directory.Delete(ociDir, true);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_MixedDockerAndOciImages_BothProcessedAsync()
    {
        var componentRecorder = new ComponentRecorder();

        var ociDir = Path.Combine(Path.GetTempPath(), "test-oci-mixed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ociDir);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [NodeLatestImage, $"oci-dir:{ociDir}"],
                componentRecorder
            );

            var syftOutputJson = """
                {
                    "distro": { "id": "azurelinux", "versionID": "3.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "userInput": "/oci-image",
                            "imageID": "sha256:ociimg",
                            "tags": [],
                            "repoDigests": [],
                            "layers": [
                                { "digest": "sha256:ocilayer1", "size": 10000 }
                            ],
                            "labels": {}
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            var ociLayerMappedComponents = new[]
            {
                new LayerMappedLinuxComponents
                {
                    DockerLayer = new DockerLayer { DiffId = "sha256:ocilayer1", LayerIndex = 0 },
                    Components = [new LinuxComponent("azurelinux", "3.0", "curl", "8.0")],
                },
            };

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns(ociLayerMappedComponents);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            // Both Docker and OCI images should have results
            scanResult.ContainerDetails.Should().HaveCount(2);

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
            detectedComponents.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(ociDir, true);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_OciLayoutImage_NoMetadata_DetectsComponentsAsync()
    {
        // Ensure that if Syft output for an OCI image is missing metadata, we can still detect components and associate them with the correct container and layers.
        var componentRecorder = new ComponentRecorder();

        var ociDir = Path.Combine(Path.GetTempPath(), "test-oci-no-meta-" + Guid.NewGuid().ToString("N")).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(ociDir);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"oci-dir:{ociDir}"],
                componentRecorder
            );

            // Syft output with no source metadata at all
            var syftOutputJson = """
                {
                    "distro": { "id": "azurelinux", "versionID": "3.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc"
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            var layerMappedComponents = new[]
            {
                new LayerMappedLinuxComponents
                {
                    DockerLayer = new DockerLayer { DiffId = "unknown", LayerIndex = 0 },
                    Components = [new LinuxComponent("azurelinux", "3.0", "curl", "8.0.0")],
                },
            };

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns(layerMappedComponents);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            scanResult.ContainerDetails.Should().ContainSingle();

            var containerDetails = scanResult.ContainerDetails.First();

            // When metadata is missing, ImageId falls back to the OCI path
            containerDetails.ImageId.Should().Be(Path.GetFullPath(ociDir));
            containerDetails.Tags.Should().BeEmpty();
            containerDetails.BaseImageRef.Should().BeEmpty();
            containerDetails.BaseImageDigest.Should().BeEmpty();

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
            detectedComponents.Should().ContainSingle();
            var detectedComponent = detectedComponents.First();
            detectedComponent.Component.Id.Should().Contain("curl");
            detectedComponent.ContainerLayerIds.Keys.Should().ContainSingle();
            var containerId = detectedComponent.ContainerLayerIds.Keys.First();
            detectedComponent.ContainerLayerIds[containerId].Should().BeEquivalentTo([0]); // Layer index from SyftOutput

            // Verify ProcessSyftOutput was called with empty layers
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.Is<IEnumerable<DockerLayer>>(layers => !layers.Any()),
                        It.IsAny<ISet<ComponentType>>()
                    ),
                Times.Once
            );
        }
        finally
        {
            Directory.Delete(ociDir, true);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_OciLayoutImage_IncompatibleMetadata_DetectsComponentsAsync()
    {
        // Ensure that if Syft output contains metadata with an incompatible schema,
        // scanning still works as if no metadata were provided.
        var componentRecorder = new ComponentRecorder();

        var ociDir = Path.Combine(Path.GetTempPath(), "test-oci-bad-meta-" + Guid.NewGuid().ToString("N")).TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(ociDir);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"oci-dir:{ociDir}"],
                componentRecorder
            );

            // Syft output with incompatible metadata (layers is a string, not an array)
            var syftOutputJson = """
                {
                    "distro": { "id": "azurelinux", "versionID": "3.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "imageID": 12345,
                            "layers": "not-an-array",
                            "tags": "also-not-an-array"
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            var layerMappedComponents = new[]
            {
                new LayerMappedLinuxComponents
                {
                    DockerLayer = new DockerLayer { DiffId = "unknown", LayerIndex = 0 },
                    Components = [new LinuxComponent("azurelinux", "3.0", "zlib", "1.2.13")],
                },
            };

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns(layerMappedComponents);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            scanResult.ContainerDetails.Should().ContainSingle();

            var containerDetails = scanResult.ContainerDetails.First();

            // Incompatible metadata is treated like missing metadata — ImageId falls back to path
            containerDetails.ImageId.Should().Be(Path.GetFullPath(ociDir));
            containerDetails.Tags.Should().BeEmpty();
            containerDetails.BaseImageRef.Should().BeEmpty();
            containerDetails.BaseImageDigest.Should().BeEmpty();

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
            detectedComponents.Should().ContainSingle();
            var detectedComponent = detectedComponents.First();
            detectedComponent.Component.Id.Should().Contain("zlib");
            detectedComponent.ContainerLayerIds.Keys.Should().ContainSingle();
            var containerId = detectedComponent.ContainerLayerIds.Keys.First();
            detectedComponent.ContainerLayerIds[containerId].Should().BeEquivalentTo([0]);
        }
        finally
        {
            Directory.Delete(ociDir, true);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_OciArchiveImage_DetectsComponentsAsync()
    {
        var componentRecorder = new ComponentRecorder();

        // Create a temp file to act as the OCI archive
        var ociArchiveDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var ociArchiveName = "test-oci-archive-" + Guid.NewGuid().ToString("N") + ".tar";
        var ociArchive = Path.Combine(ociArchiveDir, ociArchiveName);
        await System.IO.File.WriteAllBytesAsync(ociArchive, []);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"oci-archive:{ociArchive}"],
                componentRecorder
            );

            var syftOutputJson = """
                {
                    "distro": { "id": "azurelinux", "versionID": "3.0" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/oci-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "userInput": "/oci-image",
                            "imageID": "sha256:archiveimg",
                            "tags": ["myregistry.io/archived:v1"],
                            "repoDigests": [],
                            "layers": [
                                { "digest": "sha256:archivelayer1", "size": 30000 },
                                { "digest": "sha256:archivelayer2", "size": 40000 }
                            ],
                            "labels": {}
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            var layerMappedComponents = new[]
            {
                new LayerMappedLinuxComponents
                {
                    DockerLayer = new DockerLayer { DiffId = "sha256:archivelayer2", LayerIndex = 1 },
                    Components = [new LinuxComponent("azurelinux", "3.0", "openssl", "3.1.0")],
                },
            };

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns(layerMappedComponents);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            scanResult.ContainerDetails.Should().ContainSingle();

            var containerDetails = scanResult.ContainerDetails.First();
            containerDetails.ImageId.Should().Be("sha256:archiveimg");
            containerDetails.Tags.Should().ContainSingle().Which.Should().Be("myregistry.io/archived:v1");

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
            detectedComponents.Should().ContainSingle();
            var detectedComponent = detectedComponents.First();
            detectedComponent.Component.Id.Should().Contain("openssl");
            detectedComponent.ContainerLayerIds.Keys.Should().ContainSingle();
            var containerId = detectedComponent.ContainerLayerIds.Keys.First();
            detectedComponent.ContainerLayerIds[containerId].Should().BeEquivalentTo([1]); // Layer index from SyftOutput

            // Verify GetSyftOutputAsync was called with oci-archive: prefix
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.GetSyftOutputAsync(
                        It.Is<ImageReference>(r => r.Kind == ImageReferenceKind.OciArchive && r.Reference == ociArchive),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

            // Verify ProcessSyftOutput was called with the correct layers
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.Is<IEnumerable<DockerLayer>>(layers =>
                            layers.Count() == 2 &&
                            layers.First().DiffId == "sha256:archivelayer1" &&
                            layers.Last().DiffId == "sha256:archivelayer2"
                        ),
                        It.IsAny<ISet<ComponentType>>()
                    ),
                Times.Once
            );
        }
        finally
        {
            System.IO.File.Delete(ociArchive);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_DockerArchiveImage_DetectsComponentsAsync()
    {
        var componentRecorder = new ComponentRecorder();

        // Create a temp file to act as the Docker archive
        var dockerArchiveDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var dockerArchiveName = "test-docker-archive-" + Guid.NewGuid().ToString("N") + ".tar";
        var dockerArchive = Path.Combine(dockerArchiveDir, dockerArchiveName);
        await System.IO.File.WriteAllBytesAsync(dockerArchive, []);

        try
        {
            var scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                (_, __) => false,
                this.mockLogger.Object,
                null,
                [$"docker-archive:{dockerArchive}"],
                componentRecorder
            );

            var syftOutputJson = """
                {
                    "distro": { "id": "ubuntu", "versionID": "22.04" },
                    "artifacts": [],
                    "source": {
                        "id": "sha256:abc",
                        "name": "/local-image",
                        "type": "image",
                        "version": "sha256:abc",
                        "metadata": {
                            "userInput": "/local-image",
                            "imageID": "sha256:dockerarchiveimg",
                            "tags": ["myapp:v2"],
                            "repoDigests": [],
                            "layers": [
                                { "digest": "sha256:dockerlayer1", "size": 50000 },
                                { "digest": "sha256:dockerlayer2", "size": 60000 }
                            ],
                            "labels": {}
                        }
                    }
                }
                """;
            var syftOutput = SyftOutput.FromJson(syftOutputJson);

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.GetSyftOutputAsync(
                        It.IsAny<ImageReference>(),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(syftOutput);

            var layerMappedComponents = new[]
            {
                new LayerMappedLinuxComponents
                {
                    DockerLayer = new DockerLayer { DiffId = "sha256:dockerlayer1", LayerIndex = 0 },
                    Components = [new LinuxComponent("ubuntu", "22.04", "libc6", "2.35-0ubuntu3")],
                },
            };

            this.mockSyftLinuxScanner.Setup(scanner =>
                    scanner.ProcessSyftOutput(
                        It.IsAny<SyftOutput>(),
                        It.IsAny<IEnumerable<DockerLayer>>(),
                        It.IsAny<ISet<ComponentType>>()
                    )
                )
                .Returns(layerMappedComponents);

            var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

            var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
            scanResult.ContainerDetails.Should().ContainSingle();

            var containerDetails = scanResult.ContainerDetails.First();
            containerDetails.ImageId.Should().Be("sha256:dockerarchiveimg");
            containerDetails.Tags.Should().ContainSingle().Which.Should().Be("myapp:v2");

            var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
            detectedComponents.Should().ContainSingle();
            var detectedComponent = detectedComponents.First();
            detectedComponent.Component.Id.Should().Contain("libc6");
            detectedComponent.ContainerLayerIds.Keys.Should().ContainSingle();
            var containerId = detectedComponent.ContainerLayerIds.Keys.First();
            detectedComponent.ContainerLayerIds[containerId].Should().BeEquivalentTo([0]);

            // Verify GetSyftOutputAsync was called with docker-archive: prefix
            this.mockSyftLinuxScanner.Verify(
                scanner =>
                    scanner.GetSyftOutputAsync(
                        It.Is<ImageReference>(r => r.Kind == ImageReferenceKind.DockerArchive && r.Reference == dockerArchive),
                        It.IsAny<LinuxScannerScope>(),
                        It.IsAny<ISyftRunner>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
        finally
        {
            System.IO.File.Delete(dockerArchive);
        }
    }

    [TestMethod]
    public async Task TestLinuxContainerDetector_ImageParseFailure_ContinuesScanningOtherImagesAsync()
    {
        var componentRecorder = new ComponentRecorder();

        // "oci-dir:" with no path will cause ImageReference.Parse to throw
        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            (_, __) => false,
            this.mockLogger.Object,
            null,
            ["oci-dir:", NodeLatestImage],
            componentRecorder
        );

        var linuxContainerDetector = new LinuxContainerDetector(
            this.mockSyftLinuxScanner.Object,
            this.mockDockerSyftRunner.Object,
            this.mockBinarySyftRunnerFactory.Object,
            this.mockDockerService.Object,
            this.mockLinuxContainerDetectorLogger.Object
        );

        var scanResult = await linuxContainerDetector.ExecuteDetectorAsync(scanRequest);

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        scanResult.ContainerDetails.Should().ContainSingle();

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().ContainSingle();
        detectedComponents.First().Component.Id.Should().Be(BashPackageId);

        // Verify the warning was logged for the failed parse with the correct message
        this.mockLinuxContainerDetectorLogger.Verify(
            logger =>
                logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) => v.ToString()!.Contains("Failed to parse image reference 'oci-dir:'")
                    ),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
                ),
            Times.Once
        );
    }
}

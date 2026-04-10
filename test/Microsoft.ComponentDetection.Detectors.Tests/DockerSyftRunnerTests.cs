namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DockerSyftRunnerTests
{
    private readonly Mock<IDockerService> mockDockerService;
    private readonly Mock<ILogger<DockerSyftRunner>> mockLogger;

    public DockerSyftRunnerTests()
    {
        this.mockDockerService = new Mock<IDockerService>();
        this.mockLogger = new Mock<ILogger<DockerSyftRunner>>();
    }

    [TestMethod]
    public async Task CanRunAsync_ReturnsTrueWhenDockerCanRunLinuxContainers()
    {
        this.mockDockerService.Setup(s =>
                s.CanRunLinuxContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);

        var result = await runner.CanRunAsync();

        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task CanRunAsync_ReturnsFalseWhenDockerCannotRunLinuxContainers()
    {
        this.mockDockerService.Setup(s =>
                s.CanRunLinuxContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);

        var result = await runner.CanRunAsync();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task RunSyftAsync_DockerImage_PassesReferenceDirectly()
    {
        this.mockDockerService.Setup(s =>
                s.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(("{}", string.Empty));

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);
        var imageRef = new ImageReference
        {
            OriginalInput = "ubuntu:22.04",
            Reference = "ubuntu:22.04",
            Kind = ImageReferenceKind.DockerImage,
        };

        await runner.RunSyftAsync(imageRef, ["--quiet", "--output", "json"]);

        this.mockDockerService.Verify(
            s => s.CreateAndRunContainerAsync(
                It.IsAny<string>(),
                It.Is<IList<string>>(cmd => cmd[0] == "ubuntu:22.04" && cmd[1] == "--from" && cmd[2] == "docker"),
                It.Is<IList<string>>(binds => binds.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunSyftAsync_OciLayout_MountsDirectoryAndUsesMountPoint()
    {
        this.mockDockerService.Setup(s =>
                s.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(("{}", string.Empty));

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);
        var imageRef = new ImageReference
        {
            OriginalInput = "oci-dir:/path/to/oci",
            Reference = "/path/to/oci",
            Kind = ImageReferenceKind.OciLayout,
        };

        await runner.RunSyftAsync(imageRef, ["--quiet", "--output", "json"]);

        this.mockDockerService.Verify(
            s => s.CreateAndRunContainerAsync(
                It.IsAny<string>(),
                It.Is<IList<string>>(cmd => cmd[0] == "/image" && cmd[1] == "--from" && cmd[2] == "oci-dir"),
                It.Is<IList<string>>(binds =>
                    binds.Count == 1 && binds[0] == "/path/to/oci:/image:ro"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunSyftAsync_OciArchive_MountsParentDirAndUsesFileName()
    {
        this.mockDockerService.Setup(s =>
                s.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(("{}", string.Empty));

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);
        var imageRef = new ImageReference
        {
            OriginalInput = "oci-archive:/archives/image.tar",
            Reference = "/archives/image.tar",
            Kind = ImageReferenceKind.OciArchive,
        };

        await runner.RunSyftAsync(imageRef, ["--quiet", "--output", "json"]);

        var expectedDir = Path.GetDirectoryName("/archives/image.tar");
        this.mockDockerService.Verify(
            s => s.CreateAndRunContainerAsync(
                It.IsAny<string>(),
                It.Is<IList<string>>(cmd => cmd[0] == "/image/image.tar" && cmd[1] == "--from" && cmd[2] == "oci-archive"),
                It.Is<IList<string>>(binds =>
                    binds.Count == 1 && binds[0] == $"{expectedDir}:/image:ro"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunSyftAsync_DockerArchive_MountsParentDirAndUsesFileName()
    {
        this.mockDockerService.Setup(s =>
                s.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(("{}", string.Empty));

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);
        var imageRef = new ImageReference
        {
            OriginalInput = "docker-archive:/saves/myimage.tar",
            Reference = "/saves/myimage.tar",
            Kind = ImageReferenceKind.DockerArchive,
        };

        await runner.RunSyftAsync(imageRef, ["--quiet", "--output", "json"]);

        var expectedDir = Path.GetDirectoryName("/saves/myimage.tar");
        this.mockDockerService.Verify(
            s => s.CreateAndRunContainerAsync(
                It.IsAny<string>(),
                It.Is<IList<string>>(cmd => cmd[0] == "/image/myimage.tar" && cmd[1] == "--from" && cmd[2] == "docker-archive"),
                It.Is<IList<string>>(binds =>
                    binds.Count == 1 && binds[0] == $"{expectedDir}:/image:ro"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunSyftAsync_OciLayout_PreservesCaseSensitivePath()
    {
        this.mockDockerService.Setup(s =>
                s.CreateAndRunContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<IList<string>>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(("{}", string.Empty));

        var runner = new DockerSyftRunner(this.mockDockerService.Object, this.mockLogger.Object);
        var imageRef = new ImageReference
        {
            OriginalInput = "oci-dir:/Path/To/MyImage",
            Reference = "/Path/To/MyImage",
            Kind = ImageReferenceKind.OciLayout,
        };

        await runner.RunSyftAsync(imageRef, ["--quiet", "--output", "json"]);

        this.mockDockerService.Verify(
            s => s.CreateAndRunContainerAsync(
                It.IsAny<string>(),
                It.IsAny<IList<string>>(),
                It.Is<IList<string>>(binds =>
                    binds.Count == 1 && binds[0] == "/Path/To/MyImage:/image:ro"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

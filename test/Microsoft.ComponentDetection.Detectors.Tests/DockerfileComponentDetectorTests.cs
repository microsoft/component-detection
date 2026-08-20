#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Dockerfile;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DockerfileComponentDetectorTests : BaseDetectorTest<DockerfileComponentDetector>
{
    public DockerfileComponentDetectorTests() =>
        this.DetectorTestUtility
            .AddServiceMock(new Mock<ICommandLineInvocationService>())
            .AddServiceMock(new Mock<IEnvironmentVariableService>());

    [TestMethod]
    public async Task TestDockerfile_SingleFromInstructionAsync()
    {
        var dockerfile = @"
FROM nginx:1.21
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef!.Repository.Should().Be("library/nginx");
        dockerRef.Tag.Should().Be("1.21");
        dockerRef.Digest.Should().BeNull();
    }

    [TestMethod]
    public async Task TestDockerfile_FromWithRegistryAsync()
    {
        var dockerfile = @"
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef!.Domain.Should().Be("mcr.microsoft.com");
        dockerRef.Repository.Should().Be("dotnet/sdk");
        dockerRef.Tag.Should().Be("8.0");
        dockerRef.Digest.Should().BeNull();
    }

    [TestMethod]
    public async Task TestDockerfile_FromWithDigestAsync()
    {
        var dockerfile = @"
FROM nginx@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef!.Digest.Should().Be("sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
        dockerRef.Tag.Should().BeNull();
    }

    [TestMethod]
    public async Task TestDockerfile_MultiStageBuildAsync()
    {
        var dockerfile = @"
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT [""/app/MyApp""]
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);

        var repos = components
            .Select(c => c.Component as DockerReferenceComponent)
            .Where(c => c != null)
            .Select(c => c!.Repository)
            .ToList();
        repos.Should().Contain("dotnet/sdk");
        repos.Should().Contain("dotnet/runtime-deps");
    }

    [TestMethod]
    public async Task TestDockerfile_CopyFromStageNameDoesNotCreateExtraComponentAsync()
    {
        // COPY --from=<stage> references a previous build stage and should not yield a separate image component.
        var dockerfile = @"
FROM nginx:1.21 AS build
FROM alpine:3.18 AS runtime
COPY --from=build /etc/nginx/nginx.conf /etc/nginx/nginx.conf
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents().ToList();

        // Two FROM instructions => two images. The COPY --from=build should resolve back to nginx:1.21,
        // which is already registered, so no new component is added.
        components.Should().HaveCount(2);
        var repos = components
            .Select(c => (c.Component as DockerReferenceComponent)!.Repository)
            .ToList();
        repos.Should().Contain("library/nginx");
        repos.Should().Contain("library/alpine");
    }

    [TestMethod]
    public async Task TestDockerfile_CopyFromExternalImageAsync()
    {
        // COPY --from=<image> references an image directly and should produce a component.
        var dockerfile = @"
FROM alpine:3.18
COPY --from=busybox:1.36 /bin/busybox /usr/local/bin/busybox
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents().ToList();
        components.Should().HaveCount(2);

        var repos = components
            .Select(c => (c.Component as DockerReferenceComponent)!.Repository)
            .ToList();
        repos.Should().Contain("library/alpine");
        repos.Should().Contain("library/busybox");
    }

    [TestMethod]
    public async Task TestDockerfile_LowercaseFilenameAsync()
    {
        var dockerfile = @"FROM redis:7-alpine";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDockerfile_ExtensionFilenameAsync()
    {
        var dockerfile = @"FROM redis:7-alpine";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("app.dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDockerfile_PrefixedFilenameAsync()
    {
        var dockerfile = @"FROM redis:7-alpine";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile.prod", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDockerfile_NoFromInstructionsAsync()
    {
        var dockerfile = @"
# This Dockerfile has no FROM instructions
ARG BUILD_VERSION=1.0
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestDockerfile_MalformedContentAsync()
    {
        // Garbage content should not crash the detector.
        var dockerfile = "this is not a dockerfile at all { ] : >";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestDockerfile_FromWithUnresolvedArgVariableIsSkippedAsync()
    {
        // References containing unresolved variable placeholders (e.g. ${BASE_TAG}) cannot be parsed
        // into a concrete image identity and are skipped by DockerReferenceUtility.
        var dockerfile = @"
ARG BASE_TAG=1.21
FROM nginx:${BASE_TAG}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }
}

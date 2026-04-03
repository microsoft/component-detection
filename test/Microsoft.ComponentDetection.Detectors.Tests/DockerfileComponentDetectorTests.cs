#nullable disable
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
    private readonly Mock<ICommandLineInvocationService> mockCommandLineInvocationService = new();
    private readonly Mock<IEnvironmentVariableService> mockEnvironmentVariableService = new();

    [TestInitialize]
    public void TestInitialize()
    {
        this.DetectorTestUtility
            .AddServiceMock(this.mockCommandLineInvocationService)
            .AddServiceMock(this.mockEnvironmentVariableService);
    }

    [TestMethod]
    public async Task TestDockerfile_SimpleFromInstructionAsync()
    {
        var dockerfile = "FROM nginx:1.21\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("library/nginx");
        dockerRef.Tag.Should().Be("1.21");
    }

    [TestMethod]
    public async Task TestDockerfile_MultiStageFromAsync()
    {
        var dockerfile = @"FROM node:18-alpine AS build
RUN npm ci
FROM nginx:1.21
COPY --from=build /app/dist /usr/share/nginx/html
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestDockerfile_FullRegistryAsync()
    {
        var dockerfile = "FROM gcr.io/my-project/my-app:latest\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Domain.Should().Be("gcr.io");
        dockerRef.Repository.Should().Be("my-project/my-app");
        dockerRef.Tag.Should().Be("latest");
    }

    [TestMethod]
    public async Task TestDockerfile_WithDigestAsync()
    {
        var dockerfile = "FROM nginx@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Digest.Should().Be("sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
    }

    [TestMethod]
    public async Task TestDockerfile_WithTagAndDigestAsync()
    {
        var dockerfile = "FROM nginx:1.21@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Tag.Should().Be("1.21");
        dockerRef.Digest.Should().Be("sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
    }

    [TestMethod]
    public async Task TestDockerfile_CopyFromExternalImageAsync()
    {
        var dockerfile = @"FROM nginx:1.21
COPY --from=busybox:1.35 /bin/busybox /bin/busybox
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestDockerfile_CopyFromNamedStageNotDuplicatedAsync()
    {
        var dockerfile = @"FROM node:18-alpine AS builder
RUN echo hello
FROM nginx:1.21
COPY --from=builder /app /app
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // COPY --from=builder should resolve to the existing stage, not register a new component
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestDockerfile_UnresolvedVariableSkippedAsync()
    {
        var dockerfile = @"ARG BASE_IMAGE=nginx
FROM ${BASE_IMAGE}:latest
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Unresolved variables are skipped
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestDockerfile_ScratchBaseImageAsync()
    {
        var dockerfile = "FROM scratch\nCOPY myapp /\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // scratch is a valid Docker base but it resolves to docker.io/library/scratch
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDockerfile_EmptyFileAsync()
    {
        var dockerfile = "# just a comment\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("dockerfile", dockerfile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestDockerfile_NamedDockerfilePatternAsync()
    {
        var dockerfile = "FROM alpine:3.18\n";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("app.dockerfile", dockerfile, ["*.dockerfile"])
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("library/alpine");
        dockerRef.Tag.Should().Be("3.18");
    }
}

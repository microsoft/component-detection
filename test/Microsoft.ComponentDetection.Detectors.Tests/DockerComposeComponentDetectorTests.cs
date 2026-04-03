#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.DockerCompose;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DockerComposeComponentDetectorTests : BaseDetectorTest<DockerComposeComponentDetector>
{
    [TestMethod]
    public async Task TestCompose_SingleServiceImageAsync()
    {
        var composeYaml = @"
version: '3'
services:
  web:
    image: nginx:1.21
    ports:
      - ""80:80""
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yml", composeYaml)
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
    public async Task TestCompose_MultipleServicesAsync()
    {
        var composeYaml = @"
version: '3'
services:
  web:
    image: nginx:1.21
  db:
    image: postgres:15
  cache:
    image: redis:7-alpine
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task TestCompose_FullRegistryImageAsync()
    {
        var composeYaml = @"
services:
  app:
    image: ghcr.io/myorg/myapp:v2.0
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("compose.yaml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Domain.Should().Be("ghcr.io");
        dockerRef.Repository.Should().Be("myorg/myapp");
        dockerRef.Tag.Should().Be("v2.0");
    }

    [TestMethod]
    public async Task TestCompose_BuildOnlyServiceIgnoredAsync()
    {
        var composeYaml = @"
version: '3'
services:
  app:
    build: ./app
    ports:
      - ""3000:3000""
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestCompose_MixedBuildAndImageAsync()
    {
        var composeYaml = @"
version: '3'
services:
  app:
    build: ./app
  db:
    image: postgres:15
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("library/postgres");
        dockerRef.Tag.Should().Be("15");
    }

    [TestMethod]
    public async Task TestCompose_NoServicesKeyAsync()
    {
        var composeYaml = @"
version: '3'
networks:
  frontend:
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestCompose_ImageWithDigestAsync()
    {
        var composeYaml = @"
services:
  app:
    image: nginx@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yaml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Digest.Should().Be("sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
    }

    [TestMethod]
    public async Task TestCompose_ImageWithTagAndDigestAsync()
    {
        var composeYaml = @"
services:
  app:
    image: nginx:1.21@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.yaml", composeYaml)
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
    public async Task TestCompose_OverrideFileAsync()
    {
        var composeYaml = @"
services:
  web:
    image: myregistry.io/web:latest
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("docker-compose.override.yml", composeYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();
    }
}

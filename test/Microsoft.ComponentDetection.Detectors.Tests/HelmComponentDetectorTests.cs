#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Helm;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class HelmComponentDetectorTests : BaseDetectorTest<HelmComponentDetector>
{
    [TestMethod]
    public async Task TestHelm_DirectImageStringAsync()
    {
        var valuesYaml = @"
replicaCount: 1
image: nginx:1.21
service:
  type: ClusterIP
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
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
    public async Task TestHelm_StructuredImageReferenceAsync()
    {
        var valuesYaml = @"
replicaCount: 1
image:
  repository: myregistry.io/myapp
  tag: ""2.0.0""
service:
  type: ClusterIP
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("myapp");
        dockerRef.Domain.Should().Be("myregistry.io");
        dockerRef.Tag.Should().Be("2.0.0");
    }

    [TestMethod]
    public async Task TestHelm_StructuredImageWithRegistryAsync()
    {
        var valuesYaml = @"
image:
  registry: ghcr.io
  repository: org/myimage
  tag: ""v1.0""
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Domain.Should().Be("ghcr.io");
        dockerRef.Repository.Should().Be("org/myimage");
        dockerRef.Tag.Should().Be("v1.0");
    }

    [TestMethod]
    public async Task TestHelm_NestedImageReferencesAsync()
    {
        var valuesYaml = @"
app:
  frontend:
    image: nginx:1.21
  backend:
    image:
      repository: node
      tag: ""18-alpine""
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestHelm_EmptyValuesYamlAsync()
    {
        var valuesYaml = @"
replicaCount: 1
service:
  type: ClusterIP
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestHelm_ChartYamlIgnoredAsync()
    {
        var chartYaml = @"
apiVersion: v2
name: my-chart
version: 0.1.0
dependencies:
  - name: postgresql
    version: ""11.0.0""
    repository: https://charts.bitnami.com/bitnami
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", chartYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestHelm_ImageWithDigestAsync()
    {
        var valuesYaml = @"
image:
  repository: nginx
  digest: ""sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1""
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Digest.Should().Be("sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
    }

    [TestMethod]
    public async Task TestHelm_ImagesInSequenceAsync()
    {
        var valuesYaml = @"
sidecars:
  - name: sidecar1
    image: busybox:1.35
  - name: sidecar2
    image: alpine:3.18
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }
}

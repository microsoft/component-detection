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
    private const string MinimalChartYaml = @"
apiVersion: v2
name: my-chart
version: 0.1.0
";

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
            .WithFile("Chart.yaml", MinimalChartYaml)
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
            .WithFile("Chart.yaml", MinimalChartYaml)
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
            .WithFile("Chart.yaml", MinimalChartYaml)
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
            .WithFile("Chart.yaml", MinimalChartYaml)
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
            .WithFile("Chart.yaml", MinimalChartYaml)
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
    public async Task TestHelm_ValuesWithoutChartYamlSkippedAsync()
    {
        var valuesYaml = @"
image: nginx:1.21
";

        // No Chart.yaml provided — the values file should be skipped entirely.
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("values.yaml", valuesYaml)
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
            .WithFile("Chart.yaml", MinimalChartYaml)
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
    public async Task TestHelm_StructuredImageWithTagAndDigestAsync()
    {
        var valuesYaml = @"
image:
  repository: nginx
  tag: ""1.21""
  digest: ""sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1""
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
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
    public async Task TestHelm_DirectImageStringWithDigestAsync()
    {
        var valuesYaml = @"
image: nginx@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
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
    public async Task TestHelm_DirectImageStringWithTagAndDigestAsync()
    {
        var valuesYaml = @"
image: nginx:1.21@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
            .WithFile("values.yaml", valuesYaml)
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
            .WithFile("Chart.yaml", MinimalChartYaml)
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestHelm_UnresolvedVariableSkippedAsync()
    {
        var valuesYaml = @"
image: ${REGISTRY}/app:${TAG}
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
            .WithFile("values.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestHelm_ValuesYmlExtensionAsync()
    {
        var valuesYaml = @"
image: nginx:1.21
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
            .WithFile("values.yml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestHelm_ValuesOverrideFileAsync()
    {
        var valuesYaml = @"
image: redis:7-alpine
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
            .WithFile("values.production.yaml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestHelm_CustomValuesFilenameAsync()
    {
        var valuesYaml = @"
image: postgres:15
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yaml", MinimalChartYaml)
            .WithFile("myapp-values-dev.yml", valuesYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestHelm_LowercaseChartYamlAsync()
    {
        var chartYaml = @"
apiVersion: v2
name: my-chart
version: 0.1.0
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("chart.yaml", chartYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestHelm_ChartYmlExtensionAsync()
    {
        var chartYaml = @"
apiVersion: v2
name: my-chart
version: 0.1.0
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Chart.yml", chartYaml)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }
}

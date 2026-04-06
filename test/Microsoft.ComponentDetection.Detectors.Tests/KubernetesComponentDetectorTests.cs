namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Kubernetes;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class KubernetesComponentDetectorTests : BaseDetectorTest<KubernetesComponentDetector>
{
    [TestMethod]
    public async Task TestK8s_PodWithContainerImageAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: my-pod
spec:
  containers:
    - name: web
      image: nginx:1.21
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pod.yaml", manifest)
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
    public async Task TestK8s_DeploymentWithMultipleContainersAsync()
    {
        var manifest = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-app
spec:
  replicas: 3
  selector:
    matchLabels:
      app: my-app
  template:
    metadata:
      labels:
        app: my-app
    spec:
      containers:
        - name: app
          image: myregistry.io/myapp:v2.0
        - name: sidecar
          image: busybox:1.35
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("deployment.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestK8s_InitContainersAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: my-pod
spec:
  initContainers:
    - name: init
      image: busybox:1.35
  containers:
    - name: app
      image: nginx:1.21
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pod.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestK8s_CronJobAsync()
    {
        var manifest = @"
apiVersion: batch/v1
kind: CronJob
metadata:
  name: my-cronjob
spec:
  schedule: ""*/5 * * * *""
  jobTemplate:
    spec:
      template:
        spec:
          containers:
            - name: job
              image: python:3.11-slim
          restartPolicy: OnFailure
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("cronjob.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("library/python");
        dockerRef.Tag.Should().Be("3.11-slim");
    }

    [TestMethod]
    public async Task TestK8s_NonKubernetesYamlIgnoredAsync()
    {
        var manifest = @"
name: my-config
settings:
  debug: true
  image: nginx:latest
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("config.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestK8s_ServiceIgnoredAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Service
metadata:
  name: my-service
spec:
  selector:
    app: my-app
  ports:
    - port: 80
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("service.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestK8s_StatefulSetAsync()
    {
        var manifest = @"
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: my-db
spec:
  serviceName: my-db
  replicas: 3
  selector:
    matchLabels:
      app: my-db
  template:
    metadata:
      labels:
        app: my-db
    spec:
      containers:
        - name: db
          image: postgres:15
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("statefulset.yaml", manifest)
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
    public async Task TestK8s_DaemonSetAsync()
    {
        var manifest = @"
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: fluentd
spec:
  selector:
    matchLabels:
      name: fluentd
  template:
    metadata:
      labels:
        name: fluentd
    spec:
      containers:
        - name: fluentd
          image: fluentd:v1.16
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("daemonset.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("library/fluentd");
        dockerRef.Tag.Should().Be("v1.16");
    }

    [TestMethod]
    public async Task TestK8s_ImageWithFullRegistryAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: my-pod
spec:
  containers:
    - name: app
      image: gcr.io/my-project/my-app:latest
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pod.yaml", manifest)
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
    public async Task TestK8s_EmptyContainersAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: my-pod
spec:
  containers: []
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pod.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestK8s_ImageWithDigestOnlyAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: my-pod
spec:
  containers:
    - name: app
      image: nginx@sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pod.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().ContainSingle();

        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Digest.Should().Be("sha256:abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
    }

    [TestMethod]
    public async Task TestK8s_UnresolvedVariablesSkippedAsync()
    {
        var manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: my-pod
spec:
  containers:
    - name: app
      image: ${REGISTRY}/app:${TAG}
    - name: sidecar
      image: nginx:1.21
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pod.yaml", manifest)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();

        // Only the literal image reference (nginx:1.21) should be registered;
        // the variable-interpolated image (${REGISTRY}/app:${TAG}) should be silently skipped.
        components.Should().ContainSingle();
        var dockerRef = components.First().Component as DockerReferenceComponent;
        dockerRef.Should().NotBeNull();
        dockerRef.Repository.Should().Be("library/nginx");
        dockerRef.Tag.Should().Be("1.21");
    }
}

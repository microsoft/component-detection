namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GoComponentWithReplaceDetectorTests : BaseDetectorTest<GoComponentWithReplaceDetector>
{
    private readonly Mock<ICommandLineInvocationService> commandLineMock;
    private readonly Mock<IEnvironmentVariableService> envVarService;

    public GoComponentWithReplaceDetectorTests()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(false);
        this.DetectorTestUtility.AddServiceMock(this.commandLineMock);

        this.envVarService = new Mock<IEnvironmentVariableService>();
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(true);
        this.DetectorTestUtility.AddServiceMock(this.envVarService);
    }

    [TestMethod]
    public async Task TestGoModDetectorWithValidFile_ReturnsSuccessfullyAsync()
    {
        var goMod =
            @"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
    gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127
    github.com/dgrijalva/jwt-go v3.2.0+incompatible
)";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/dgrijalva/jwt-go v3.2.0+incompatible - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestGoModDetector_CommentsOnFile_CommentsAreIgnoredAsync()
    {
        var goMod =
            @"module github.com/Azure/azure-storage-blob-go

require (
    // comment
    github.com/kr/pretty v0.1.0 // indirect
)";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle("there is only one component definition on the file");

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestGoSumDetectorWithValidFile_ReturnsSuccessfullyAsync()
    {
        var goSum =
            @"
github.com/golang/mock v1.1.1/go.mod h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A=
github.com/golang/mock v1.2.0/go.mod h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A=
github.com/golang/protobuf v0.0.0-20161109072736-4bd1920723d7/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
github.com/golang/protobuf v1.2.0/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
github.com/golang/protobuf v1.3.1 h1:YF8+flBXS5eO826T4nzqPrxfhQThhXl0YzfuUPu4SBg=
github.com/golang/protobuf v1.3.1/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
github.com/golang/protobuf v1.3.2 h1:6nsPYzhq5kReh6QImI3k5qWzO4PEbvbIW2cwSfR/6xs=
github.com/golang/protobuf v1.3.2/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=
)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.sum", goSum)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(6);
        var typedComponents = detectedComponents.Select(d => d.Component).ToList();
        typedComponents.Should().Contain(
            new GoComponent("github.com/golang/mock", "v1.1.1", "h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A="));
        typedComponents.Should().Contain(
            new GoComponent("github.com/golang/mock", "v1.2.0", "h1:oTYuIxOrZwtPieC+H1uAHpcLFnEyAGVDL/k47Jfbm0A="));
        typedComponents.Should().Contain(
            new GoComponent("github.com/golang/protobuf", "v0.0.0-20161109072736-4bd1920723d7", "h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U="));
        typedComponents.Should().Contain(
            new GoComponent("github.com/golang/protobuf", "v1.2.0", "h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U="));
        typedComponents.Should().Contain(
            new GoComponent("github.com/golang/protobuf", "v1.3.1", "h1:YF8+flBXS5eO826T4nzqPrxfhQThhXl0YzfuUPu4SBg="));
        typedComponents.Should().Contain(
            new GoComponent("github.com/golang/protobuf", "v1.3.2", "h1:6nsPYzhq5kReh6QImI3k5qWzO4PEbvbIW2cwSfR/6xs="));
    }

    [TestMethod]
    public async Task TestGoModDetector_MultipleSpaces_ReturnsSuccessfullyAsync()
    {
        var goMod =
            @"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go      v0.2.1
    github.com/kr/pretty    v0.1.0 // indirect
    gopkg.in/check.v1   v1.0.0-20180628173108-788fd7840127
    github.com/dgrijalva/jwt-go     v3.2.0+incompatible
)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/dgrijalva/jwt-go v3.2.0+incompatible - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestGoModDetector_ComponentsWithMultipleLocations_ReturnsSuccessfullyAsync()
    {
        var goMod1 =
            @"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
    gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127
    github.com/Azure/go-autorest v10.15.2+incompatible
)";
        var goMod2 =
            @"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
    gopkg.in/check.v1 v1.0.0-20180628173108-788fd7840127
    github.com/Azure/go-autorest v10.15.2+incompatible
)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod1)
            .WithFile("go.mod", goMod2, fileLocation: Path.Join(Path.GetTempPath(), "another-location", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Count().Should().Be(4);

        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
        dependencyGraphs.Keys.Should().HaveCount(2);

        var firstGraph = dependencyGraphs.Values.First();
        var secondGraph = dependencyGraphs.Values.Skip(1).First();

        firstGraph.GetComponents().Should().BeEquivalentTo(secondGraph.GetComponents());
    }

    [TestMethod]
    public async Task TestGoModDetectorInvalidFiles_DoesNotFailAsync()
    {
        var invalidGoMod =
            @"     #/bin/sh
lorem ipsum
four score and seven bugs ago
$#26^#25%4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", invalidGoMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Count().Should().Be(0);
    }

    [TestMethod]
    public async Task TestGoModDetector_SkipsGoSumFilesAsync()
    {
        var goMod =
            @"module contoso.com/greetings
go 1.18

require github.com/go-sql-driver/mysql v1.7.1 // indirect";

        var goSum =
            @"github.com/go-sql-driver/mysql v1.7.1 h1:lUIinVbN1DY0xBg0eMOzmmtGoHwWBbvnWubQUrtU8EI=
github.com/go-sql-driver/mysql v1.7.1/go.mod h1:OXbVy3sEdcQ2Doequ6Z5BW6fXNQTmx+9S1MCJN5yJMI=
github.com/golang/protobuf v1.2.0/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .WithFile("go.mod", goMod, new[] { "go.mod" })
            .WithFile("go.sum", goSum)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();

        var component = componentRecorder.GetDetectedComponents().First();
        component.Component.Id.Should().Be("github.com/go-sql-driver/mysql v1.7.1 - Go");
    }

    [TestMethod]
    public async Task TestGoModDetector_HandlesTwoRequiresSectionsAsync()
    {
        var goMod =
            @"module microsoft/component-detection

go 1.18

require (
        github.com/go-sql-driver/mysql v1.7.1
        rsc.io/quote v1.5.2
)

require (
        golang.org/x/text v0.0.0-20170915032832-14c0d48ead0c // indirect
        rsc.io/sampler v1.3.0 // indirect
)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(4);

        var expectedComponentIds = new[]
        {
            "github.com/go-sql-driver/mysql v1.7.1 - Go", "rsc.io/quote v1.5.2 - Go",
            "golang.org/x/text v0.0.0-20170915032832-14c0d48ead0c - Go", "rsc.io/sampler v1.3.0 - Go",
        };

        componentRecorder.GetDetectedComponents().Select(c => c.Component.Id).Should().BeEquivalentTo(expectedComponentIds);
    }

    [TestMethod]
    public async Task TestGoSumDetection_TwoEntriesForTheSameComponent_ReturnsSuccessfullyAsync()
    {
        var goSum =
            @"
github.com/exponent-io/jsonpath v0.0.0-20151013193312-d6023ce2651d h1:105gxyaGwCFad8crR9dcMQWvV9Hvulu6hwUh4tWPJnM=
github.com/exponent-io/jsonpath v0.0.0-20151013193312-d6023ce2651d/go.mod h1:ZZMPRZwes7CROmyNKgQzC3XPs6L/G2EJLHddWejkmf4=
)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.sum", goSum)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestGoModDetector_DetectorOnlyDetectInsideRequireSectionAsync()
    {
        var goMod =
            @"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/kr/pretty v0.1.0 // indirect
)
replace (
	github.com/Azure/go-autorest => github.com/Azure/go-autorest v13.3.2+incompatible
	github.com/docker/distribution => github.com/docker/distribution v0.0.0-20191216044856-a8371794149d
)
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/kr/pretty v0.1.0 - Go").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestGoModDetector_DetectReplaceSectionAsync()
    {
        var goMod =
            @"module github.com/Azure/azure-storage-blob-go

require (
    github.com/Azure/azure-pipeline-go v0.2.1
    github.com/docker/distribution v2.7.1+incompatible
    github.com/Masterminds/sprig/v3 v3.1.0

)
replace (
	github.com/Azure/go-autorest => github.com/Azure/go-autorest v13.3.2+incompatible
	github.com/docker/distribution => github.com/docker/distribution v0.0.0-20191216044856-a8371794149d
)
";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "github.com/Azure/azure-pipeline-go v0.2.1 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/Masterminds/sprig/v3 v3.1.0 - Go").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "github.com/docker/distribution v0.0.0-20191216044856-a8371794149d - Go").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestGoModDetector_SkipsGoSumFilesWithReplaceAsync()
    {
        var goMod =
            @"module contoso.com/greetings
go 1.18

require github.com/go-sql-driver/mysql v1.7.1 // indirect
replace github.com/go-sql-driver/mysql => github.com/go-sql-driver/mysql v1.7.3";

        var goSum =
            @"github.com/go-sql-driver/mysql v1.7.1 h1:lUIinVbN1DY0xBg0eMOzmmtGoHwWBbvnWubQUrtU8EI=
github.com/go-sql-driver/mysql v1.7.1/go.mod h1:OXbVy3sEdcQ2Doequ6Z5BW6fXNQTmx+9S1MCJN5yJMI=
github.com/golang/protobuf v1.2.0/go.mod h1:6lQm79b+lXiMfvg/cZm0SGofjICqVBUtrP5yJMmIC1U=";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .WithFile("go.mod", goMod, new[] { "go.mod" })
            .WithFile("go.sum", goSum)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();

        var component = componentRecorder.GetDetectedComponents().First();
        component.Component.Id.Should().Be("github.com/go-sql-driver/mysql v1.7.3 - Go");
    }

    [TestMethod]
    public async Task TestGoDetector_GoCommandNotFoundAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(false);

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfullyAsync();
    }

    [TestMethod]
    public async Task TestGoDetector_GoCommandThrowsAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(() => throw new InvalidOperationException("Some horrible error occured"));

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfullyAsync();
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphCommandFailsAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go mod graph", null, It.IsAny<DirectoryInfo>(), It.IsAny<string>()))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 1,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfullyAsync();
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphCommandThrowsAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go mod graph", null, It.IsAny<DirectoryInfo>(), It.IsAny<string>()))
            .ReturnsAsync(() => throw new InvalidOperationException("Some horrible error occured"));

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfullyAsync();
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphReplaceAsync()
    {
        var buildDependencies = @"{
    ""Path"": ""some-package"",
    ""Version"": ""v1.2.3"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11"",
    ""Replace"": {
                    ""Path"": ""some-package"",
                    ""Version"": ""v1.2.4"",
                    ""Time"": ""2021-12-06T23:04:27Z"",
                    ""Indirect"": true,
                    ""GoMod"": ""C:\\test\\go.mod"",
                    ""GoVersion"": ""1.11"",
                }
}" + "\n" + @"{
    ""Path"": ""test"",
    ""Version"": ""v2.0.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""other"",
    ""Version"": ""v1.2.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""a"",
    ""Version"": ""v1.5.0"",
    ""Time"": ""2020-05-19T17:02:07Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}";
        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 a@v1.5.0";

        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), new[] { "list", "-mod=readonly", "-m", "-json", "all" }))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = buildDependencies,
            });

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);
        detectedComponents.Should().NotContain(component => component.Component.Id == "other v1.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "other v1.2.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "some-package v1.2.4 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "test v2.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "a v1.5.0 - Go");
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphHappyPathAsync()
    {
        var buildDependencies = @"{
    ""Path"": ""some-package"",
    ""Version"": ""v1.2.3"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""test"",
    ""Version"": ""v2.0.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""other"",
    ""Version"": ""v1.2.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""a"",
    ""Version"": ""v1.5.0"",
    ""Time"": ""2020-05-19T17:02:07Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}";
        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 a@v1.5.0";

        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), new[] { "list", "-mod=readonly", "-m", "-json", "all" }))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = buildDependencies,
            });

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);
        detectedComponents.Should().NotContain(component => component.Component.Id == "other v1.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "other v1.2.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "some-package v1.2.3 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "test v2.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "a v1.5.0 - Go");
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphCyclicDependenciesAsync()
    {
        var buildDependencies = @"{
    ""Path"": ""github.com/prometheus/common"",
    ""Version"": ""v0.32.1"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""github.com/prometheus/client_golang"",
    ""Version"": ""v1.11.0"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}" + "\n" + @"{
    ""Path"": ""github.com/prometheus/client_golang"",
    ""Version"": ""v1.12.1"",
    ""Time"": ""2021-12-06T23:04:27Z"",
    ""Indirect"": true,
    ""GoMod"": ""C:\\test\\go.mod"",
    ""GoVersion"": ""1.11""
}";
        var goGraph = @"
github.com/prometheus/common@v0.32.1 github.com/prometheus/client_golang@v1.11.0
github.com/prometheus/client_golang@v1.12.1 github.com/prometheus/common@v0.32.1";
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), new[] { "list", "-mod=readonly", "-m", "-json", "all" }))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = buildDependencies,
            });

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), new[] { "mod", "graph" }))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task TestGoDetector_GoCliRequiresEnvVarToRunAsync()
    {
        await this.TestGoSumDetectorWithValidFile_ReturnsSuccessfullyAsync();

        this.commandLineMock.Verify(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()), Times.Never);
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GoComponentDetectorTests : BaseDetectorTest<GoComponentDetector>
{
    private readonly Mock<ICommandLineInvocationService> commandLineMock;
    private readonly Mock<IEnvironmentVariableService> envVarService;
    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;
    private readonly Mock<ILogger<GoComponentDetector>> mockLogger;
    private readonly Mock<IGoParserFactory> mockParserFactory;
    private readonly Mock<IGoParser> mockGoModParser;
    private readonly Mock<IGoParser> mockGoSumParser;
    private readonly Mock<IGoParser> mockGoCliParser;

    public GoComponentDetectorTests()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        this.mockGoModParser = new Mock<IGoParser>();
        this.mockGoSumParser = new Mock<IGoParser>();
        this.mockGoCliParser = new Mock<IGoParser>();
        this.mockParserFactory = new Mock<IGoParserFactory>();

        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<string[]>()))
            .ReturnsAsync(false);
        this.DetectorTestUtility.AddServiceMock(this.commandLineMock);
        this.envVarService = new Mock<IEnvironmentVariableService>();
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(true);
        this.DetectorTestUtility.AddServiceMock(this.envVarService);
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
        this.mockLogger = new Mock<ILogger<GoComponentDetector>>();
        this.DetectorTestUtility.AddServiceMock(this.mockLogger);
        this.DetectorTestUtility.AddServiceMock(this.mockParserFactory);
    }

    private void SetupMockGoModParser()
    {
        this.mockParserFactory.Setup(f => f.CreateParser(GoParserType.GoMod, It.IsAny<ILogger>())).Returns(this.mockGoModParser.Object);
    }

    private void SetupMockGoSumParser()
    {
        this.mockParserFactory.Setup(f => f.CreateParser(GoParserType.GoSum, It.IsAny<ILogger>())).Returns(this.mockGoSumParser.Object);
    }

    private void SetupMockGoCLIParser()
    {
        this.mockParserFactory.Setup(f => f.CreateParser(GoParserType.GoCLI, It.IsAny<ILogger>())).Returns(this.mockGoCliParser.Object);
    }

    private void SetupActualGoModParser()
    {
        this.mockParserFactory.Setup(f => f.CreateParser(GoParserType.GoMod, It.IsAny<ILogger>())).Returns(new GoModParser(this.mockLogger.Object));
    }

    private void SetupActualGoSumParser()
    {
        this.mockParserFactory.Setup(f => f.CreateParser(GoParserType.GoSum, It.IsAny<ILogger>())).Returns(new GoSumParser(this.mockLogger.Object));
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

        this.SetupActualGoModParser();
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
        this.SetupActualGoModParser();
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

        this.SetupActualGoSumParser();
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

        this.SetupActualGoModParser();
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
        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod1)
            .WithFile("go.mod", goMod2, fileLocation: Path.Join(Path.GetTempPath(), "another-location", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(4);

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

        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", invalidGoMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
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

        this.SetupActualGoModParser();
        this.SetupActualGoSumParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .WithFile("go.mod", goMod, ["go.mod"])
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

        this.SetupActualGoModParser();
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

        this.SetupActualGoSumParser();
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
        this.SetupActualGoModParser();
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
        var goMod = @"
module example.com/my/module

go 1.11

require (
    some-package v1.2.3 // indirect
    test v2.0.0 // indirect
    other v1.2.0 // indirect
    a v1.5.0 // indirect
)

replace some-package v1.2.3 => some-package v1.2.4
;";
        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 a@v1.5.0";

        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, It.IsAny<CancellationToken>(), cmdParams))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "go version go1.24.3 windows/amd64" });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), cmdParams))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);
        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
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
        var goMod = @"
module example.com/my/module

go 1.11

require (
    some-package v1.2.3 // indirect
    test v2.0.0 // indirect
    other v1.2.0 // indirect
    a v1.5.0 // indirect
);
";
        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 a@v1.5.0";

        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, It.IsAny<CancellationToken>(), cmdParams))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "go version go1.24.3 windows/amd64" });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), cmdParams))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);
        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
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
        var goMod = @"
    module example.com/my/module

go 1.11

require (
    github.com/prometheus/common v0.32.1 // indirect
    github.com/prometheus/client_golang v1.12.1 // indirect
github.com/prometheus/client_golang v1.11.0 // indirect
)
";
        var goGraph = @"
github.com/prometheus/common@v0.32.1 github.com/prometheus/client_golang@v1.11.0
github.com/prometheus/client_golang@v1.12.1 github.com/prometheus/common@v0.32.1";
        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, It.IsAny<CancellationToken>(), cmdParams))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "go version go1.24.3 windows/amd64" });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), cmdParams))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
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

    [TestMethod]
    public async Task TestGoDetector_GoGraphReplaceWithRelativePathAsync()
    {
        var localPath = OperatingSystem.IsWindows()
    ? "C:/test/module/"
    : "/home/test/module/";
        var goMod = $@"module example.com/project

go 1.11

require (
    some-package v1.2.3 // indirect
    test v2.0.0         // indirect
    other v1.2.0        // indirect
    a v1.5.0            // indirect
)

replace a v1.5.0 => {localPath}
";

        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 a@v1.5.0";
        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, It.IsAny<CancellationToken>(), cmdParams))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "go version go1.24.3 windows/amd64" });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), cmdParams))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        this.fileUtilityServiceMock.Setup(fs => fs.Exists(It.IsAny<string>()))
            .Returns(true);
        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        detectedComponents.Should().NotContain(component => component.Component.Id == "a v1.5.0 - Go");
        detectedComponents.Should().NotContain(component => component.Component.Id == "other v1.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "other v1.2.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "test v2.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "some-package v1.2.3 - Go");
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphReplaceMultipleReplaceModulesAsync()
    {
        var goMod = @"
module example.com/project

go 1.17

require (
    some-package v1.2.3 // indirect
    test v2.0.0         // indirect
    other v1.2.0        // indirect
    github v1.5.0       // indirect
)

replace other v1.2.0 => ./component
replace github v1.5.0 => ./module
";

        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 github@v1.5.0";
        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, It.IsAny<CancellationToken>(), cmdParams))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "go version go1.24.3 windows/amd64" });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), cmdParams))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        this.fileUtilityServiceMock.Setup(fs => fs.Exists(It.IsAny<string>()))
            .Returns(true);

        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);
        detectedComponents.Should().NotContain(component => component.Component.Id == "a v1.5.0 - Go");
        detectedComponents.Should().NotContain(component => component.Component.Id == "other v1.0.0 - Go");
        detectedComponents.Should().NotContain(component => component.Component.Id == "other v1.2.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "test v2.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "some-package v1.2.3 - Go");
    }

    [TestMethod]
    public async Task TestGoDetector_GoGraphReplaceNoPathAsync()
    {
        var goMod = @"
module example.com/project

go 1.11

require (
    some-package v1.2.3 // indirect
    test v2.0.0         // indirect
    other v1.2.0        // indirect
    github v1.5.0       // indirect
)

replace github v1.5.0 => github v1.18
";

        var goGraph = "example.com/mainModule some-package@v1.2.3\nsome-package@v1.2.3 other@v1.0.0\nsome-package@v1.2.3 other@v1.2.0\ntest@v2.0.0 github@v1.5.0";
        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), cmdParams))
            .ReturnsAsync(true);
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, It.IsAny<CancellationToken>(), cmdParams))
                .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "go version go1.24.3 windows/amd64" });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), cmdParams))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = goGraph,
            });

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);
        this.SetupActualGoModParser();
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);
        detectedComponents.Should().NotContain(component => component.Component.Id == "other v1.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "github v1.18 - Go");
        detectedComponents.Should().NotContain(component => component.Component.Id == "github v1.5.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "other v1.2.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "test v2.0.0 - Go");
        detectedComponents.Should().ContainSingle(component => component.Component.Id == "some-package v1.2.3 - Go");
        this.mockLogger.Verify(
           logger => logger.Log(
               LogLevel.Information,
               It.IsAny<EventId>(),
               It.Is<It.IsAnyType>((v, t) => v.ToString().Equals("go Module github-v1.5.0 being replaced with github-v1.18")),
               It.IsAny<Exception>(),
               It.IsAny<Func<It.IsAnyType, Exception, string>>()),
           Times.Never);
    }

    [TestMethod]
    public async Task GoModDetector_GoCliIs1_11OrGreater_GoGraphIsExecutedAsync()
    {
        var goMod = string.Empty;
        this.SetupMockGoModParser();

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        string[] cmdParams = [];
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, null, It.IsAny<string[]>()))
            .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = "go version go1.23.6 windows/amd64",
            });

        cmdParams = ["mod", "graph"];
        this.commandLineMock.Setup(service => service.ExecuteCommandAsync(
            "go",
            null,
            It.IsAny<DirectoryInfo>(),
            default,
            It.Is<string[]>(args => args.Length == 2 && args[0] == "mod" && args[1] == "graph")))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = string.Empty,
        })
        .Verifiable();

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.commandLineMock.Verify();
    }

    [TestMethod]
    public async Task GoModDetector_GoCliIs111OrLessThan1_11_GoGraphIsNotExecutedAsync()
    {
        var goMod = string.Empty;
        this.SetupMockGoModParser();

        string[] cmdParams = [];
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, null, It.IsAny<string[]>()))
        .ReturnsAsync(true);

        cmdParams = ["version"];
        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, cmdParams))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = "go version go1.10.6 windows/amd64",
        });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", goMod)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.commandLineMock.Verify(
            service => service.ExecuteCommandAsync(
                "go",
                null,
                It.IsAny<DirectoryInfo>(),
                default,
                It.Is<string[]>(args => args.Length == 2 && args[0] == "mod" && args[1] == "graph")),
            times: Times.Never);
    }

    [TestMethod]
    public async Task GoModDetector_GoModFileFound_GoModParserIsExecuted()
    {
        var goModParserMock = new Mock<IGoParser>();
        this.mockParserFactory.Setup(x => x.CreateParser(GoParserType.GoMod, It.IsAny<ILogger>())).Returns(goModParserMock.Object);

        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, null, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(new CommandLineExecutionResult
        {
            ExitCode = 0,
            StdOut = "go version go1.10.6 windows/amd64",
        });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        goModParserMock.Verify(parser => parser.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()), Times.Once);
    }

    /// <summary>
    /// Verifies that if Go CLI is enabled/available and succeeds, go.sum file is not parsed and vice-versa.
    /// </summary>
    /// <returns>Task.</returns>
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task GoDetector_GoSum_GoSumParserExecuted(bool goCliSucceeds)
    {
        var nInvocationsOfSumParser = goCliSucceeds ? 0 : 1;

        this.SetupMockGoSumParser();
        this.SetupMockGoCLIParser();
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);

        // Setup go cli parser to succeed/fail
        this.mockGoCliParser.Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>())).ReturnsAsync(goCliSucceeds);

        // Setup go sum parser to succeed
        this.mockGoSumParser.Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>())).ReturnsAsync(true);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.sum", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.mockParserFactory.Verify(clm => clm.CreateParser(GoParserType.GoSum, It.IsAny<ILogger>()), nInvocationsOfSumParser == 0 ? Times.Never : Times.Once);
    }

    /// <summary>
    /// Verifies that if Go CLI is disabled, go.sum is parsed.
    /// </summary>
    /// <returns>Task.</returns>
    [TestMethod]
    public async Task GoDetector_GoSum_GoSumParserExecutedIfCliDisabled()
    {
        var goSumParserMock = new Mock<IGoParser>();
        this.mockParserFactory.Setup(x => x.CreateParser(GoParserType.GoSum, It.IsAny<ILogger>())).Returns(goSumParserMock.Object);

        // Setup environment variable to disable CLI scan
        this.envVarService.Setup(s => s.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(true);

        // Setup go sum parser to succed
        goSumParserMock.Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>())).ReturnsAsync(true);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.sum", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.mockParserFactory.Verify(clm => clm.CreateParser(GoParserType.GoSum, It.IsAny<ILogger>()), Times.Once);
    }

    [TestMethod]
    public async Task GoModDetector_ExecutingGoVersionFails_DetectorDoesNotFail()
    {
        this.SetupMockGoModParser();

        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync("go", null, null, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .ReturnsAsync(true);

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync("go", null, null, default, It.Is<string[]>(p => p.SequenceEqual(new List<string> { "version" }.ToArray()))))
        .Throws(new InvalidOperationException("Failed to execute go version"));

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.mockGoModParser.Verify(parser => parser.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()), Times.Once);
    }

    [TestMethod]
    public async Task GoModDetector_VerifyLocalReferencesIgnored()
    {
        var goModFilePath = "./TestFiles/go_WithLocalReferences.mod"; // Replace with your actual file path
        var fileStream = new FileStream(goModFilePath, FileMode.Open, FileAccess.Read);

        var goModParser = new GoModParser(this.mockLogger.Object);
        var mockSingleFileComponentRecorder = new Mock<ISingleFileComponentRecorder>();

        var capturedComponents = new List<DetectedComponent>();
        var expectedComponentIds = new List<string>()
        {
            "github.com/grafana/grafana-app-sdk v0.22.1 - Go",
            "k8s.io/kube-openapi v1.1.1 - Go",
        };

        mockSingleFileComponentRecorder
        .Setup(m => m.RegisterUsage(
            It.IsAny<DetectedComponent>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<bool?>(),
            It.IsAny<DependencyScope?>(),
            It.IsAny<string>()))
        .Callback<DetectedComponent, bool, string, bool?, DependencyScope?, string>((comp, _, _, _, _, _) =>
        {
            capturedComponents.Add(comp);
        });

        var mockComponentStream = new Mock<IComponentStream>();
        mockComponentStream.Setup(mcs => mcs.Stream).Returns(fileStream);
        mockComponentStream.Setup(mcs => mcs.Location).Returns("Location");

        var result = await goModParser.ParseAsync(mockSingleFileComponentRecorder.Object, mockComponentStream.Object, new GoGraphTelemetryRecord());
        result.Should().BeTrue();
        capturedComponents
        .Select(c => c.Component.Id)
        .Should()
        .BeEquivalentTo(expectedComponentIds);
    }

    /// <summary>
    /// Verify that nested directories are skipped once root is processed.
    /// Assume root GoModVersion is >= 1.17.
    /// </summary>
    /// <returns>Task.</returns>
    [TestMethod]
    public async Task GoDetector_GoMod_VerifyNestedRootsUnderGTE117_AreSkipped()
    {
        var processedFiles = new List<string>();
        this.SetupMockGoModParser();
        this.mockGoModParser
            .Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()))
            .ReturnsAsync(true)
            .Callback<ISingleFileComponentRecorder, IComponentStream, GoGraphTelemetryRecord>((_, file, record) =>
            {
                processedFiles.Add(file.Location);
                record.GoModVersion = "1.18";
            });

        var root = Path.Combine("C:", "root");
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "d", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "a", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        processedFiles.Should().ContainSingle();
        processedFiles.Should().OnlyContain(p => p == Path.Combine(root, "go.mod"));
    }

    /// <summary>
    /// Verify that nested roots under go mod less than 1.17 are not skipped.
    /// </summary>
    /// <returns>Task.</returns>
    [TestMethod]
    public async Task GoDetector_GoMod_VerifyNestedRootsUnderLT117AreNotSkipped()
    {
        var root = Path.Combine("C:", "root");
        var processedFiles = new List<string>();
        this.SetupMockGoModParser();
        this.mockGoModParser
            .Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()))
            .ReturnsAsync(true)
            .Callback<ISingleFileComponentRecorder, IComponentStream, GoGraphTelemetryRecord>((_, file, record) =>
            {
                processedFiles.Add(file.Location);
                var rootMod = Path.Combine(root, "go.mod");
                var aMod = Path.Combine(root, "a", "go.mod");
                var bMod = Path.Combine(root, "b", "go.mod");
                record.GoModVersion = file.Location switch
                {
                    var loc when loc == rootMod => "1.16",
                    var loc when loc == aMod => "1.16",
                    var loc when loc == bMod => "1.17",
                    _ => null,
                };
            });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "d", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "a", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        processedFiles.Should().HaveCount(5);
        processedFiles.Should().ContainInOrder(
            Path.Combine(root, "go.mod"),
            Path.Combine(root, "a", "go.mod"),
            Path.Combine(root, "b", "go.mod"),
            Path.Combine(root, "a", "a", "go.mod"),
            Path.Combine(root, "a", "b", "go.mod"));
    }

    /// <summary>
    /// Verify that nested roots are not skipped if parent go.mod parsing fails.
    /// </summary>
    /// <returns>Task.</returns>
    [TestMethod]
    public async Task GoDetector_GoMod_VerifyNestedRootsAreNotSkippedIfParentParseFails()
    {
        var processedFiles = new List<string>();
        var root = Path.Combine("C:", "root");
        this.SetupMockGoModParser();

        this.mockGoModParser
            .Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()))
            .ReturnsAsync((ISingleFileComponentRecorder recorder, IComponentStream file, GoGraphTelemetryRecord record) =>
            {
                processedFiles.Add(file.Location);
                var aMod = Path.Combine(root, "a", "go.mod");
                var bMod = Path.Combine(root, "b", "go.mod");
                record.GoModVersion = file.Location switch
                {
                    var loc when loc == bMod => "1.18",
                    _ => "1.16",
                };

                // Simulate parse failure only for C:\root\a\go.mod
                if (file.Location == aMod)
                {
                    return false;
                }

                return true;
            });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "d", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "a", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        processedFiles.Should().HaveCount(5);
        processedFiles.Should().ContainInOrder(
            Path.Combine(root, "go.mod"),
            Path.Combine(root, "a", "go.mod"),
            Path.Combine(root, "b", "go.mod"),
            Path.Combine(root, "a", "a", "go.mod"),
            Path.Combine(root, "a", "b", "go.mod"));
    }

    /// <summary>
    /// Verify that nested directories are skipped once root is processed.
    /// Assume root GoModVersion is >= 1.17.
    /// </summary>
    /// <returns>Task.</returns>
    [TestMethod]
    public async Task GoDetector_GoSum_VerifyNestedRootsUnderGoSum_AreSkipped()
    {
        var processedFiles = new List<string>();
        var root = Path.Combine("C:", "root");
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);
        this.SetupMockGoCLIParser();
        this.mockGoCliParser
            .Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()))
            .ReturnsAsync(true)
            .Callback<ISingleFileComponentRecorder, IComponentStream, GoGraphTelemetryRecord>((_, file, record) =>
            {
                processedFiles.Add(file.Location);
            });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "go.mod"))
            .WithFile("go.sum", string.Empty, fileLocation: Path.Combine(root, "go.sum"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "d", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "a", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        processedFiles.Should().ContainSingle();
        processedFiles.Should().OnlyContain(p => p == Path.Combine(root, "go.sum"));
    }

    [TestMethod]
    public async Task GoDetector_GoSum_VerifyNestedRootsAreNotSkippedIfParentParseFails()
    {
        var processedFiles = new List<string>();
        var root = Path.Combine("C:", "root");
        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("DisableGoCliScan")).Returns(false);
        this.SetupMockGoModParser();
        this.SetupMockGoCLIParser();
        this.SetupMockGoSumParser();

        this.mockGoModParser
            .Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()))
            .ReturnsAsync((ISingleFileComponentRecorder recorder, IComponentStream file, GoGraphTelemetryRecord record) =>
            {
                processedFiles.Add(file.Location);
                var bMod = Path.Combine(root, "b", "go.mod");
                record.GoModVersion = file.Location switch
                {
                    var loc when loc == bMod => "1.18",
                    _ => "1.16",
                };

                return true;
            });

        this.mockGoCliParser
            .Setup(p => p.ParseAsync(It.IsAny<ISingleFileComponentRecorder>(), It.IsAny<IComponentStream>(), It.IsAny<GoGraphTelemetryRecord>()))
            .ReturnsAsync((ISingleFileComponentRecorder recorder, IComponentStream file, GoGraphTelemetryRecord record) =>
            {
                processedFiles.Add(file.Location);
                return file.Location != Path.Combine(root, "a", "go.sum");
            });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("go.sum", string.Empty, fileLocation: Path.Combine(root, "a", "go.sum"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "a", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "a", "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "d", "go.mod"))
            .WithFile("go.mod", string.Empty, fileLocation: Path.Combine(root, "b", "a", "go.mod"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        processedFiles.Should().HaveCount(5);
        processedFiles.Should().ContainInOrder(
            Path.Combine(root, "go.mod"),
            Path.Combine(root, "a", "go.sum"),
            Path.Combine(root, "b", "go.mod"),
            Path.Combine(root, "a", "a", "go.mod"),
            Path.Combine(root, "a", "b", "go.mod"));
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GoCLIParserTests
{
    private GoCLIParser parser;
    private Mock<ICommandLineInvocationService> commandLineMock;
    private Mock<IFileUtilityService> fileUtilityMock;
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        this.fileUtilityMock = new Mock<IFileUtilityService>();
        this.loggerMock = new Mock<ILogger>();

        this.parser = new GoCLIParser(
            this.loggerMock.Object,
            this.fileUtilityMock.Object,
            this.commandLineMock.Object);
    }

    [TestMethod]
    public async Task ParseAsync_GoCliNotAvailable_ReturnsFalse()
    {
        // Arrange
        this.SetupGoAvailable(false);
        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, _) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeFalse();
        record.IsGoAvailable.Should().BeFalse();
    }

    [TestMethod]
    public async Task ParseAsync_GoListCommandFails_ReturnsFalse()
    {
        // Arrange
        this.SetupGoAvailable(true);
        this.SetupGoListCommand(stdOut: string.Empty, exitCode: 1, stdErr: "go: error loading module");

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, _) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeFalse();
        record.DidGoCliCommandFail.Should().BeTrue();
        record.GoCliCommandError.Should().Be("go: error loading module");
    }

    [TestMethod]
    public async Task ParseAsync_SingleDirectDependency_RegistersWithExplicitFlag()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true },
            new GoBuildModuleTestData { Path = "github.com/dep/one", Version = "v1.2.3", Indirect = false });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        captured.Should().ContainSingle();

        var (component, isExplicit) = captured[0];
        var goComponent = component.Component as GoComponent;
        goComponent.Name.Should().Be("github.com/dep/one");
        goComponent.Version.Should().Be("v1.2.3");
        isExplicit.Should().BeTrue();
    }

    [TestMethod]
    public async Task ParseAsync_SingleIndirectDependency_RegistersWithoutExplicitFlag()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true },
            new GoBuildModuleTestData { Path = "github.com/dep/indirect", Version = "v2.0.0", Indirect = true });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        captured.Should().ContainSingle();

        var (component, isExplicit) = captured[0];
        var goComponent = component.Component as GoComponent;
        goComponent.Name.Should().Be("github.com/dep/indirect");
        isExplicit.Should().BeFalse();
    }

    [TestMethod]
    public async Task ParseAsync_MainModuleSkipped_NotRegistered()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        captured.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ParseAsync_ReplaceWithVersion_UsesReplacementModule()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true },
            new GoBuildModuleTestData
            {
                Path = "github.com/original/pkg",
                Version = "v1.0.0",
                Replace = new GoBuildModuleTestData { Path = "github.com/fork/pkg", Version = "v1.1.0" },
            });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        captured.Should().ContainSingle();

        var goComponent = captured[0].Component.Component as GoComponent;
        goComponent.Name.Should().Be("github.com/fork/pkg");
        goComponent.Version.Should().Be("v1.1.0");
    }

    [TestMethod]
    public async Task ParseAsync_ReplaceWithLocalPath_FileExists_SkipsModule()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true },
            new GoBuildModuleTestData
            {
                Path = "github.com/replaced/pkg",
                Version = "v1.0.0",
                Replace = new GoBuildModuleTestData { Path = "../local/module" },
            });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        // Local go.mod file exists
        this.fileUtilityMock.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        captured.Should().BeEmpty(); // Local replacement should be skipped
    }

    [TestMethod]
    public async Task ParseAsync_ReplaceWithLocalPath_FileNotExists_RegistersOriginalModule()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true },
            new GoBuildModuleTestData
            {
                Path = "github.com/replaced/pkg",
                Version = "v1.0.0",
                Replace = new GoBuildModuleTestData { Path = "../missing/module" },
            });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        // Local go.mod file does NOT exist
        this.fileUtilityMock.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();

        // Should still register the original module since local replacement is invalid
        captured.Should().ContainSingle();
        var goComponent = captured[0].Component.Component as GoComponent;
        goComponent.Name.Should().Be("github.com/replaced/pkg");
        goComponent.Version.Should().Be("v1.0.0");
    }

    [TestMethod]
    public async Task ParseAsync_MultipleDependencies_RegistersAll()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true },
            new GoBuildModuleTestData { Path = "github.com/dep/one", Version = "v1.0.0", Indirect = false },
            new GoBuildModuleTestData { Path = "github.com/dep/two", Version = "v2.0.0", Indirect = true },
            new GoBuildModuleTestData { Path = "github.com/dep/three", Version = "v3.0.0", Indirect = false });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, captured) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        captured.Should().HaveCount(3);

        var names = captured.Select(c => (c.Component.Component as GoComponent).Name).ToList();
        names.Should().Contain("github.com/dep/one");
        names.Should().Contain("github.com/dep/two");
        names.Should().Contain("github.com/dep/three");
    }

    [TestMethod]
    public async Task ParseAsync_Success_PopulatesTelemetryRecord()
    {
        // Arrange
        this.SetupGoAvailable(true);
        var goListOutput = BuildGoListJsonOutput(
            new GoBuildModuleTestData { Path = "example.com/main", Version = "v1.0.0", Main = true });
        this.SetupGoListCommand(goListOutput);
        this.SetupGoModGraphCommand(string.Empty, exitCode: 0);

        var stream = CreateComponentStream("/project/go.mod");
        var record = new GoGraphTelemetryRecord();
        var (recorderMock, _) = CreateCapturingRecorder();

        // Act
        var result = await this.parser.ParseAsync(recorderMock.Object, stream, record);

        // Assert
        result.Should().BeTrue();
        record.IsGoAvailable.Should().BeTrue();
        record.DidGoCliCommandFail.Should().BeFalse();
        record.ProjectRoot.Should().Be("/project");
        record.WasGraphSuccessful.Should().BeTrue();
    }

    /// <summary>
    /// Creates a mock IComponentStream with the specified location.
    /// </summary>
    private static IComponentStream CreateComponentStream(string location)
    {
        var mock = new Mock<IComponentStream>();
        mock.Setup(s => s.Location).Returns(location);
        return mock.Object;
    }

    /// <summary>
    /// Creates a mock ISingleFileComponentRecorder that captures registered components.
    /// </summary>
    private static (Mock<ISingleFileComponentRecorder> Mock, List<(DetectedComponent Component, bool IsExplicit)> Captured) CreateCapturingRecorder()
    {
        var captured = new List<(DetectedComponent Component, bool IsExplicit)>();
        var mock = new Mock<ISingleFileComponentRecorder>();

        mock.Setup(r => r.RegisterUsage(
                It.IsAny<DetectedComponent>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool?>(),
                It.IsAny<DependencyScope?>(),
                It.IsAny<string>()))
            .Callback<DetectedComponent, bool, string, bool?, DependencyScope?, string>(
                (component, isExplicit, parentId, isDev, scope, framework) =>
                {
                    captured.Add((component, isExplicit));
                });

        return (mock, captured);
    }

    /// <summary>
    /// Builds a JSON output string for go list -m -json all command.
    /// Each module is a separate JSON object (not an array).
    /// </summary>
    private static string BuildGoListJsonOutput(params GoBuildModuleTestData[] modules)
    {
        var sb = new StringBuilder();
        foreach (var module in modules)
        {
            sb.AppendLine(module.ToJson());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Sets up the command line mock to indicate Go CLI is available.
    /// </summary>
    private void SetupGoAvailable(bool isAvailable = true)
    {
        this.commandLineMock
            .Setup(c => c.CanCommandBeLocatedAsync("go", null, It.IsAny<DirectoryInfo>(), It.Is<string[]>(args => args.Contains("version"))))
            .ReturnsAsync(isAvailable);
    }

    /// <summary>
    /// Sets up the go list command to return the specified output.
    /// </summary>
    private void SetupGoListCommand(string stdOut, int exitCode = 0, string stdErr = "")
    {
        this.commandLineMock
            .Setup(c => c.ExecuteCommandAsync(
                "go",
                null,
                It.IsAny<DirectoryInfo>(),
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(args => args.Contains("list") && args.Contains("-m") && args.Contains("-json") && args.Contains("all"))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = exitCode,
                StdOut = stdOut,
                StdErr = stdErr,
            });
    }

    /// <summary>
    /// Sets up the go mod graph command to return the specified output.
    /// </summary>
    private void SetupGoModGraphCommand(string stdOut, int exitCode = 0)
    {
        this.commandLineMock
            .Setup(c => c.ExecuteCommandAsync(
                "go",
                null,
                It.IsAny<DirectoryInfo>(),
                It.IsAny<CancellationToken>(),
                It.Is<string[]>(args => args.Contains("mod") && args.Contains("graph"))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = exitCode,
                StdOut = stdOut,
            });
    }

    /// <summary>
    /// Test data class for building Go module JSON.
    /// </summary>
    private class GoBuildModuleTestData
    {
        public string Path { get; set; }

        public string Version { get; set; }

        public bool Main { get; set; }

        public bool Indirect { get; set; }

        public GoBuildModuleTestData Replace { get; set; }

        public string ToJson()
        {
            var parts = new List<string>
            {
                $"\"Path\": \"{this.Path}\"",
            };

            if (this.Version != null)
            {
                parts.Add($"\"Version\": \"{this.Version}\"");
            }

            if (this.Main)
            {
                parts.Add("\"Main\": true");
            }

            if (this.Indirect)
            {
                parts.Add("\"Indirect\": true");
            }

            if (this.Replace != null)
            {
                var replaceParts = new List<string> { $"\"Path\": \"{this.Replace.Path}\"" };
                if (this.Replace.Version != null)
                {
                    replaceParts.Add($"\"Version\": \"{this.Replace.Version}\"");
                }

                parts.Add($"\"Replace\": {{ {string.Join(", ", replaceParts)} }}");
            }

            return $"{{ {string.Join(", ", parts)} }}";
        }
    }
}

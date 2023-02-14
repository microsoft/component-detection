namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MavenCommandServiceTests
{
    private readonly Mock<ICommandLineInvocationService> commandLineMock;
    private readonly Mock<IMavenStyleDependencyGraphParserService> parserServiceMock;
    private readonly MavenCommandService mavenCommandService;

    public MavenCommandServiceTests()
    {
        this.commandLineMock = new Mock<ICommandLineInvocationService>();
        var loggerMock = new Mock<ILogger>();

        this.parserServiceMock = new Mock<IMavenStyleDependencyGraphParserService>();

        this.mavenCommandService = new MavenCommandService(
            this.commandLineMock.Object,
            this.parserServiceMock.Object,
            loggerMock.Object);
    }

    [TestMethod]
    public async Task MavenCLIExists_ExpectedArguments_ReturnTrueAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(
            MavenCommandService.PrimaryCommand,
            MavenCommandService.AdditionalValidCommands,
            MavenCommandService.MvnVersionArgument)).ReturnsAsync(true);

        var result = await this.mavenCommandService.MavenCLIExistsAsync();

        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task MavenCLIExists_ExpectedArguments_ReturnFalseAsync()
    {
        this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(
            MavenCommandService.PrimaryCommand,
            MavenCommandService.AdditionalValidCommands,
            MavenCommandService.MvnVersionArgument)).ReturnsAsync(false);

        var result = await this.mavenCommandService.MavenCLIExistsAsync();

        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task GenerateDependenciesFile_SuccessAsync()
    {
        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
            },
        };

        var bcdeMvnFileName = "bcde.mvndeps";
        var cliParameters = new[] { "dependency:tree", "-B", $"-DoutputFile={bcdeMvnFileName}", "-DoutputType=text", $"-f{pomLocation}" };

        this.commandLineMock.Setup(x => x.ExecuteCommandAsync(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                It.Is<string[]>(y => this.ShouldBeEquivalentTo(y, cliParameters))))
            .ReturnsAsync(new CommandLineExecutionResult
            {
                ExitCode = 0,
            })
            .Verifiable();

        await this.mavenCommandService.GenerateDependenciesFileAsync(processRequest);

        Mock.Verify(this.commandLineMock);
    }

    [TestMethod]
    public void ParseDependenciesFile_Success()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
        var content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine}";

        var pomLocation = "Test/location";
        var processRequest = new ProcessRequest
        {
            ComponentStream = new ComponentStream
            {
                Location = pomLocation,
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            },
        };

        var lines = new[] { "com.bcde.test:top-level:jar:1.0.0", $"\\- {componentString}" };
        this.parserServiceMock.Setup(x => x.Parse(lines, It.IsAny<ISingleFileComponentRecorder>())).Verifiable();

        this.mavenCommandService.ParseDependenciesFile(processRequest);

        Mock.Verify(this.parserServiceMock);
    }

    protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
    {
        result.Should<T>().BeEquivalentTo(expected);
        return true;
    }
}

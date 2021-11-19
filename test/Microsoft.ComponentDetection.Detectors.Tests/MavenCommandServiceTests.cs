using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class MavenCommandServiceTests
    {
        private Mock<ICommandLineInvocationService> commandLineMock;
        private Mock<IMavenStyleDependencyGraphParserService> parserServiceMock;
        private MavenCommandService mavenCommandService;

        [TestInitialize]
        public void InitializeTests()
        {
            commandLineMock = new Mock<ICommandLineInvocationService>();
            var loggerMock = new Mock<ILogger>();

            parserServiceMock = new Mock<IMavenStyleDependencyGraphParserService>();

            mavenCommandService = new MavenCommandService
            {
                CommandLineInvocationService = commandLineMock.Object,
                ParserService = parserServiceMock.Object,
                Logger = loggerMock.Object,
            };
        }

        [TestMethod]
        public async Task MavenCLIExists_ExpectedArguments_ReturnTrue()
        {
            commandLineMock.Setup(x => x.CanCommandBeLocated(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                MavenCommandService.MvnVersionArgument)).ReturnsAsync(true);

            var result = await mavenCommandService.MavenCLIExists();

            result.Should().BeTrue();
        }

        [TestMethod]
        public async Task MavenCLIExists_ExpectedArguments_ReturnFalse()
        {
            commandLineMock.Setup(x => x.CanCommandBeLocated(
                MavenCommandService.PrimaryCommand,
                MavenCommandService.AdditionalValidCommands,
                MavenCommandService.MvnVersionArgument)).ReturnsAsync(false);

            var result = await mavenCommandService.MavenCLIExists();

            result.Should().BeFalse();
        }

        [TestMethod]
        public async Task GenerateDependenciesFile_Success()
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

            commandLineMock.Setup(x => x.ExecuteCommand(
                                                        MavenCommandService.PrimaryCommand,
                                                        MavenCommandService.AdditionalValidCommands,
                                                        It.Is<string[]>(y => ShouldBeEquivalentTo(y, cliParameters))))
                .ReturnsAsync(new CommandLineExecutionResult
                {
                    ExitCode = 0,
                })
                .Verifiable();

            await mavenCommandService.GenerateDependenciesFile(processRequest);

            Mock.Verify(commandLineMock);
        }

        [TestMethod]
        public void ParseDependenciesFile_Success()
        {
            const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
            string content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine}";

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
            parserServiceMock.Setup(x => x.Parse(lines, It.IsAny<ISingleFileComponentRecorder>())).Verifiable();

            mavenCommandService.ParseDependenciesFile(processRequest);

            Mock.Verify(parserServiceMock);
        }

        protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
        {
            result.Should<T>().BeEquivalentTo(expected);
            return true;
        }
    }
}

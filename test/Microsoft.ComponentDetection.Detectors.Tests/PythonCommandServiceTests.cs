using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class PythonCommandServiceTests
    {
        private Mock<ICommandLineInvocationService> commandLineInvokationService;

        [TestInitialize]
        public void TestInitialize()
        {
            this.commandLineInvokationService = new Mock<ICommandLineInvocationService>();
        }

        [TestMethod]
        public async Task PythonCommandService_ReturnsTrueWhenPythonExists()
        {
            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            Assert.IsTrue(await service.PythonExists());
        }

        [TestMethod]
        public async Task PythonCommandService_ReturnsFalseWhenPythonExists()
        {
            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);

            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            Assert.IsFalse(await service.PythonExists());
        }

        [TestMethod]
        public async Task PythonCommandService_ReturnsTrueWhenPythonExistsForAPath()
        {
            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("test", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            Assert.IsTrue(await service.PythonExists("test"));
        }

        [TestMethod]
        public async Task PythonCommandService_ReturnsFalseWhenPythonExistsForAPath()
        {
            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("test", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);

            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            Assert.IsFalse(await service.PythonExists("test"));
        }

        [TestMethod]
        public async Task PythonCommandService_ParsesEmptySetupPyOutputCorrectly()
        {
            var fakePath = @"c:\the\fake\path.py";
            var fakePathAsPassedToPython = fakePath.Replace("\\", "/");

            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
            this.commandLineInvokationService.Setup(x => x.ExecuteCommand("python", It.IsAny<IEnumerable<string>>(), It.Is<string>(c => c.Contains(fakePathAsPassedToPython))))
                                        .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "[]", StdErr = string.Empty });

            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            var result = await service.ParseFile(fakePath);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task PythonCommandService_ParsesRegularSetupPyOutputCorrectly()
        {
            var fakePath = @"c:\the\fake\path.py";
            var fakePathAsPassedToPython = fakePath.Replace("\\", "/");

            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
            this.commandLineInvokationService.Setup(x => x.ExecuteCommand("python", It.IsAny<IEnumerable<string>>(), It.Is<string>(c => c.Contains(fakePathAsPassedToPython))))
                                        .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "['knack==0.4.1', 'setuptools>=1.0,!=1.1', 'vsts-cli-common==0.1.3', 'vsts-cli-admin==0.1.3', 'vsts-cli-build==0.1.3', 'vsts-cli-code==0.1.3', 'vsts-cli-team==0.1.3', 'vsts-cli-package==0.1.3', 'vsts-cli-work==0.1.3']", StdErr = string.Empty });

            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            var result = await service.ParseFile(fakePath);
            var expected = new string[] { "knack==0.4.1", "setuptools>=1.0,!=1.1", "vsts-cli-common==0.1.3", "vsts-cli-admin==0.1.3", "vsts-cli-build==0.1.3", "vsts-cli-code==0.1.3", "vsts-cli-team==0.1.3", "vsts-cli-package==0.1.3", "vsts-cli-work==0.1.3" }.Select<string, (string, GitComponent)>(dep => (dep, null)).ToArray();

            Assert.AreEqual(9, result.Count);

            for (var i = 0; i < 9; i++)
            {
                Assert.AreEqual(expected[i], result[i]);
            }
        }

        [TestMethod]
        public async Task PythonCommandService_ParsesRequirementsTxtCorrectly()
        {
            var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            try
            {
                using (var writer = File.CreateText(testPath))
                {
                    writer.WriteLine("knack==0.4.1");
                    writer.WriteLine("vsts-cli-common==0.1.3    \\      ");
                    writer.WriteLine("    --hash=sha256:856476331f3e26598017290fd65bebe81c960e806776f324093a46b76fb2d1c0");
                    writer.Flush();
                }

                var result = await service.ParseFile(testPath);
                var expected = new string[] { "knack==0.4.1", "vsts-cli-common==0.1.3" }.Select<string, (string, GitComponent)>(dep => (dep, null)).ToArray();

                Assert.AreEqual(expected.Length, result.Count);

                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i], result[i]);
                }
            }
            finally
            {
                if (File.Exists(testPath))
                {
                    File.Delete(testPath);
                }
            }
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_CommentAreIgnored()
        {
            var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            try
            {
                using (var writer = File.CreateText(testPath))
                {
                    writer.WriteLine("#this is a comment");
                    writer.WriteLine("knack==0.4.1 #this is another comment");
                    writer.Flush();
                }

                var result = await service.ParseFile(testPath);
                (string, GitComponent) expected = ("knack==0.4.1", null);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(expected, result.First());
            }
            finally
            {
                if (File.Exists(testPath))
                {
                    File.Delete(testPath);
                }
            }
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentsSupported()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtBasicGitComponent, parseResult =>
            {
                parseResult.Count.Should().Be(1);

                var tuple = parseResult.Single();
                tuple.Item1.Should().BeNull();
                tuple.Item2.Should().NotBeNull();

                var gitComponent = tuple.Item2;
                gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
                gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentAndEnvironmentMarker()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentAndEnvironmentMarker, parseResult =>
            {
                parseResult.Count.Should().Be(1);

                var tuple = parseResult.Single();
                tuple.Item1.Should().BeNull();
                tuple.Item2.Should().NotBeNull();

                var gitComponent = tuple.Item2;
                gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
                gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentAndComment()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentAndComment, parseResult =>
            {
                parseResult.Count.Should().Be(1);

                var tuple = parseResult.Single();
                tuple.Item1.Should().BeNull();
                tuple.Item2.Should().NotBeNull();

                var gitComponent = tuple.Item2;
                gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
                gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentAndCommentAndEnvironmentMarker()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentAndCommentAndEnvironmentMarker, parseResult =>
            {
                parseResult.Count.Should().Be(1);

                var tuple = parseResult.Single();
                tuple.Item1.Should().BeNull();
                tuple.Item2.Should().NotBeNull();

                var gitComponent = tuple.Item2;
                gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
                gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentNotCreatedWhenGivenBranch()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentBranchInsteadOfCommitId, parseResult =>
            {
                parseResult.Count.Should().Be(0);
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentNotCreatedWhenGivenRelease()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentReleaseInsteadOfCommitId, parseResult =>
            {
                parseResult.Count.Should().Be(0);
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentNotCreatedWhenGivenMalformedCommitHash()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentCommitIdWrongLength, parseResult =>
            {
                parseResult.Count.Should().Be(0);
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentsMultiple()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtDoubleGitComponents, parseResult =>
            {
                parseResult.Count.Should().Be(2);

                var tuple1 = parseResult.First();
                tuple1.Item1.Should().BeNull();
                tuple1.Item2.Should().NotBeNull();

                var gitComponent1 = tuple1.Item2;
                gitComponent1.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
                gitComponent1.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");

                var tuple2 = parseResult.Skip(1).First();
                tuple2.Item1.Should().BeNull();
                tuple2.Item2.Should().NotBeNull();

                var gitComponent2 = tuple2.Item2;
                gitComponent2.RepositoryUrl.Should().Be("https://github.com/path/to/package-two");
                gitComponent2.CommitHash.Should().Be("41b95ec");
            });
        }

        [TestMethod]
        public async Task ParseFile_RequirementTxtHasComment_GitComponentWrappedInRegularComponent()
        {
            await this.SetupAndParseReqsTxt(this.requirementstxtGitComponentWrappedinRegularComponents, parseResult =>
            {
                parseResult.Count.Should().Be(3);

                var tuple1 = parseResult.First();
                tuple1.Item1.Should().NotBeNull();
                tuple1.Item2.Should().BeNull();

                var regularComponent1 = tuple1.Item1;
                regularComponent1.Should().Be("something=1.3");

                var tuple2 = parseResult.Skip(1).First();
                tuple2.Item1.Should().BeNull();
                tuple2.Item2.Should().NotBeNull();

                var gitComponent = tuple2.Item2;
                gitComponent.RepositoryUrl.Should().Be("https://github.com/path/to/package-two");
                gitComponent.CommitHash.Should().Be("41b95ec");

                var tuple3 = parseResult.ToArray()[2];
                tuple3.Item1.Should().NotBeNull();
                tuple3.Item2.Should().BeNull();

                var regularComponent2 = tuple3.Item1;
                regularComponent2.Should().Be("other=2.1");
            });
        }

        private async Task<int> SetupAndParseReqsTxt(string fileToParse, Action<IList<(string, GitComponent)>> verificationFunction)
        {
            var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

            this.commandLineInvokationService.Setup(x => x.CanCommandBeLocated("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
            var service = new PythonCommandService { CommandLineInvocationService = this.commandLineInvokationService.Object };

            using (var writer = File.CreateText(testPath))
            {
                writer.WriteLine(fileToParse);
                writer.Flush();
            }

            var result = await service.ParseFile(testPath);
            verificationFunction(result);
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }

            return 0;
        }

        private readonly string requirementstxtBasicGitComponent = @"
git+git://github.com/vscode-python/jedi-language-server@42823a2598d4b6369e9273c5ad237a48c5d67553";

        private readonly string requirementstxtGitComponentAndEnvironmentMarker = @"
git+git://github.com/vscode-python/jedi-language-server@42823a2598d4b6369e9273c5ad237a48c5d67553 ; python_version >= ""3.6""";

        private readonly string requirementstxtGitComponentAndComment = @"
git+git://github.com/vscode-python/jedi-language-server@42823a2598d4b6369e9273c5ad237a48c5d67553 # this is a comment";

        private readonly string requirementstxtGitComponentAndCommentAndEnvironmentMarker = @"
git+git://github.com/vscode-python/jedi-language-server@42823a2598d4b6369e9273c5ad237a48c5d67553 ; python_version >= {""3.6""  # via -r requirements.in";

        private readonly string requirementstxtGitComponentBranchInsteadOfCommitId = @"
git+git://github.com/path/to/package-two@master#egg=package-two";

        private readonly string requirementstxtGitComponentReleaseInsteadOfCommitId = @"
git+git://github.com/path/to/package-two@0.1#egg=package-two";

        private readonly string requirementstxtGitComponentCommitIdWrongLength = @"
git+git://github.com/vscode-python/jedi-language-server@42823a2598d4b6369e9273c5ad237a48c5d6755300000000000";

        private readonly string requirementstxtDoubleGitComponents = @"
git+git://github.com/vscode-python/jedi-language-server@42823a2598d4b6369e9273c5ad237a48c5d67553 ; python_version >= {""3.6""  # via -r requirements.in
git+git://github.com/path/to/package-two@41b95ec#egg=package-two";

        private readonly string requirementstxtGitComponentWrappedinRegularComponents = @"
something=1.3
git+git://github.com/path/to/package-two@41b95ec#egg=package-two
other=2.1";
    }
}

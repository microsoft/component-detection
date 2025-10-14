#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PythonCommandServiceTests
{
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

    private readonly Mock<ICommandLineInvocationService> commandLineInvokationService;
    private readonly Mock<ILogger<PythonCommandService>> logger;
    private readonly Mock<ILogger<PathUtilityService>> pathLogger;
    private readonly IPathUtilityService pathUtilityService;

    public PythonCommandServiceTests()
    {
        this.commandLineInvokationService = new Mock<ICommandLineInvocationService>();
        this.logger = new Mock<ILogger<PythonCommandService>>();
        this.pathLogger = new Mock<ILogger<PathUtilityService>>();
        this.pathUtilityService = new PathUtilityService(this.pathLogger.Object);
    }

    [TestMethod]
    public async Task PythonCommandService_ReturnsTrueWhenPythonExistsAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        (await service.PythonExistsAsync()).Should().BeTrue();
    }

    [TestMethod]
    public async Task PythonCommandService_ReturnsFalseWhenPythonExistsAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        (await service.PythonExistsAsync()).Should().BeFalse();
    }

    [TestMethod]
    public async Task PythonCommandService_ReturnsTrueWhenPythonExistsForAPathAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("test", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        (await service.PythonExistsAsync("test")).Should().BeTrue();
    }

    [TestMethod]
    public async Task PythonCommandService_ReturnsFalseWhenPythonExistsForAPathAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("test", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        (await service.PythonExistsAsync("test")).Should().BeFalse();
    }

    [TestMethod]
    public async Task PythonCommandService_ParsesEmptySetupPyOutputCorrectlyAsync()
    {
        var fakePath = @"c:\the\fake\path.py";
        var fakePathAsPassedToPython = this.pathUtilityService.NormalizePath(fakePath);

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync("python", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.Is<string>(c => c.Contains(fakePathAsPassedToPython))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "[]", StdErr = string.Empty });

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        var result = await service.ParseFileAsync(fakePath);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task PythonCommandService_ParsesEmptySetupPyOutputCorrectly_Python27Async()
    {
        var fakePath = @"c:\the\fake\path.py";
        var fakePathAsPassedToPython = this.pathUtilityService.NormalizePath(fakePath);

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync("python", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.Is<string>(c => c.Contains(fakePathAsPassedToPython))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "None", StdErr = string.Empty });

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        var result = await service.ParseFileAsync(fakePath);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task PythonCommandService_ParsesSetupPyOutputCorrectly_Python27NonePkgAsync()
    {
        var fakePath = @"c:\the\fake\path.py";
        var fakePathAsPassedToPython = this.pathUtilityService.NormalizePath(fakePath);

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync("python", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.Is<string>(c => c.Contains(fakePathAsPassedToPython))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "['None']", StdErr = string.Empty });

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        var result = await service.ParseFileAsync(fakePath);

        result.Should().ContainSingle();
        result.First().PackageString.Should().Be("None");
    }

    [TestMethod]
    public async Task PythonCommandService_ParsesRegularSetupPyOutputCorrectlyAsync()
    {
        var fakePath = @"c:\the\fake\path.py";
        var fakePathAsPassedToPython = this.pathUtilityService.NormalizePath(fakePath);

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync("python", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.Is<string>(c => c.Contains(fakePathAsPassedToPython))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "['knack==0.4.1', 'setuptools>=1.0,!=1.1', 'vsts-cli-common==0.1.3', 'vsts-cli-admin==0.1.3', 'vsts-cli-build==0.1.3', 'vsts-cli-code==0.1.3', 'vsts-cli-team==0.1.3', 'vsts-cli-package==0.1.3', 'vsts-cli-work==0.1.3']", StdErr = string.Empty });

        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        var result = await service.ParseFileAsync(fakePath);
        var expected = new string[] { "knack==0.4.1", "setuptools>=1.0,!=1.1", "vsts-cli-common==0.1.3", "vsts-cli-admin==0.1.3", "vsts-cli-build==0.1.3", "vsts-cli-code==0.1.3", "vsts-cli-team==0.1.3", "vsts-cli-package==0.1.3", "vsts-cli-work==0.1.3" }.Select<string, (string, GitComponent)>(dep => (dep, null)).ToArray();

        result.Should().HaveCount(9);

        for (var i = 0; i < 9; i++)
        {
            result.Should().HaveElementAt(i, expected[i]);
        }
    }

    [TestMethod]
    public async Task PythonCommandService_ParsesRequirementsTxtCorrectlyAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        try
        {
            using (var writer = File.CreateText(testPath))
            {
                await writer.WriteLineAsync("knack==0.4.1");
                await writer.WriteLineAsync("numpy==1.22.0rc3");
                await writer.WriteLineAsync("vsts-cli-common==0.1.3    \\      ");
                await writer.WriteLineAsync("    --hash=sha256:856476331f3e26598017290fd65bebe81c960e806776f324093a46b76fb2d1c0");
                await writer.FlushAsync();
            }

            var result = await service.ParseFileAsync(testPath);
            var expected = new string[] { "knack==0.4.1", "numpy==1.22.0rc3", "vsts-cli-common==0.1.3" }.Select<string, (string, GitComponent)>(dep => (dep, null)).ToArray();

            result.Should().HaveCount(expected.Length);

            for (var i = 0; i < expected.Length; i++)
            {
                result.Should().HaveElementAt(i, expected[i]);
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
    public async Task ParseFile_RequirementTxtHasComment_CommentAreIgnoredAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        try
        {
            using (var writer = File.CreateText(testPath))
            {
                await writer.WriteLineAsync("#this is a comment");
                await writer.WriteLineAsync("knack==0.4.1 #this is another comment");
                await writer.FlushAsync();
            }

            var result = await service.ParseFileAsync(testPath);
            (string, GitComponent) expected = ("knack==0.4.1", null);

            result.Should().ContainSingle();
            result.First().Should().Be(expected);
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
    public async Task ParseFile_RequirementTxtHasComment_GitComponentsSupportedAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtBasicGitComponent, parseResult =>
        {
            parseResult.Should().ContainSingle();

            var (packageString, component) = parseResult.Single();
            packageString.Should().BeNull();
            component.Should().NotBeNull();

            var gitComponent = component;
            gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
            gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentAndEnvironmentMarkerAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentAndEnvironmentMarker, parseResult =>
        {
            parseResult.Should().ContainSingle();

            var (packageString, component) = parseResult.Single();
            packageString.Should().BeNull();
            component.Should().NotBeNull();

            var gitComponent = component;
            gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
            gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentAndCommentAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentAndComment, parseResult =>
        {
            parseResult.Should().ContainSingle();

            var (packageString, component) = parseResult.Single();
            packageString.Should().BeNull();
            component.Should().NotBeNull();

            var gitComponent = component;
            gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
            gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentAndCommentAndEnvironmentMarkerAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentAndCommentAndEnvironmentMarker, parseResult =>
        {
            parseResult.Should().ContainSingle();

            var (packageString, component) = parseResult.Single();
            packageString.Should().BeNull();
            component.Should().NotBeNull();

            var gitComponent = component;
            gitComponent.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
            gitComponent.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentNotCreatedWhenGivenBranchAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentBranchInsteadOfCommitId, parseResult =>
        {
            parseResult.Should().BeEmpty();
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentNotCreatedWhenGivenReleaseAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentReleaseInsteadOfCommitId, parseResult =>
        {
            parseResult.Should().BeEmpty();
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentNotCreatedWhenGivenMalformedCommitHashAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentCommitIdWrongLength, parseResult =>
        {
            parseResult.Should().BeEmpty();
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentsMultipleAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtDoubleGitComponents, parseResult =>
        {
            parseResult.Should().HaveCount(2);

            var (packageString, component) = parseResult.First();
            packageString.Should().BeNull();
            component.Should().NotBeNull();

            var gitComponent1 = component;
            gitComponent1.RepositoryUrl.Should().Be("https://github.com/vscode-python/jedi-language-server");
            gitComponent1.CommitHash.Should().Be("42823a2598d4b6369e9273c5ad237a48c5d67553");

            var (packageString2, component2) = parseResult.Skip(1).First();
            packageString2.Should().BeNull();
            component2.Should().NotBeNull();

            var gitComponent2 = component2;
            gitComponent2.RepositoryUrl.Should().Be("https://github.com/path/to/package-two");
            gitComponent2.CommitHash.Should().Be("41b95ec");
        });
    }

    [TestMethod]
    public async Task ParseFile_RequirementTxtHasComment_GitComponentWrappedInRegularComponentAsync()
    {
        await this.SetupAndParseReqsTxtAsync(this.requirementstxtGitComponentWrappedinRegularComponents, parseResult =>
        {
            parseResult.Should().HaveCount(3);

            var (packageString, component) = parseResult.First();
            packageString.Should().NotBeNull();
            component.Should().BeNull();

            var regularComponent1 = packageString;
            regularComponent1.Should().Be("something=1.3");

            var (packageString2, component2) = parseResult.Skip(1).First();
            packageString2.Should().BeNull();
            component2.Should().NotBeNull();

            var gitComponent = component2;
            gitComponent.RepositoryUrl.Should().Be("https://github.com/path/to/package-two");
            gitComponent.CommitHash.Should().Be("41b95ec");

            var (packageString3, component3) = parseResult.ToArray()[2];
            packageString3.Should().NotBeNull();
            component3.Should().BeNull();

            var regularComponent2 = packageString3;
            regularComponent2.Should().Be("other=2.1");
        });
    }

    private async Task<int> SetupAndParseReqsTxtAsync(string fileToParse, Action<IList<(string PackageString, GitComponent Component)>> verificationFunction)
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);
        var service = new PythonCommandService(this.commandLineInvokationService.Object, this.pathUtilityService, this.logger.Object);

        using (var writer = File.CreateText(testPath))
        {
            await writer.WriteLineAsync(fileToParse);
            await writer.FlushAsync();
        }

        var result = await service.ParseFileAsync(testPath);
        verificationFunction(result);
        if (File.Exists(testPath))
        {
            File.Delete(testPath);
        }

        return 0;
    }
}

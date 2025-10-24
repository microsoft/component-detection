#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Detectors.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PipCommandServiceTests
{
    private readonly Mock<ICommandLineInvocationService> commandLineInvokationService;
    private readonly Mock<IEnvironmentVariableService> envVarService;
    private readonly Mock<IFileUtilityService> fileUtilityService;
    private readonly Mock<ILogger<PathUtilityService>> pathLogger;
    private readonly Mock<ILogger<PipCommandService>> logger;
    private readonly IPathUtilityService pathUtilityService;

    public PipCommandServiceTests()
    {
        this.commandLineInvokationService = new Mock<ICommandLineInvocationService>();
        this.pathLogger = new Mock<ILogger<PathUtilityService>>();
        this.logger = new Mock<ILogger<PipCommandService>>();
        this.pathUtilityService = new PathUtilityService(this.pathLogger.Object);
        this.envVarService = new Mock<IEnvironmentVariableService>();
        this.fileUtilityService = new Mock<IFileUtilityService>();
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsTrueWhenPipExistsAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        (await service.PipExistsAsync()).Should().BeTrue();
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsFalseWhenPipExistsAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        (await service.PipExistsAsync()).Should().BeFalse();
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsTrueWhenPythonExistsAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "-m", "pip", "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        (await service.PipExistsAsync()).Should().BeTrue();
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsTrueWhenPipExistsForAPathAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("testPath", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        (await service.PipExistsAsync("testPath")).Should().BeTrue();
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsFalseWhenPipExistsForAPathAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("testPath", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        (await service.PipExistsAsync("testPath")).Should().BeFalse();
    }

    [TestMethod]
    public async Task PipCommandService_BadVersion_ReturnsNullAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DirectoryInfo>(),
            It.IsAny<CancellationToken>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = string.Empty });

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var semVer = await service.GetPipVersionAsync();
        semVer.Should().BeNull();
    }

    [TestMethod]
    public async Task PipCommandService_BadPip_ReturnsPythonAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(false);

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            "python",
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(true);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "python",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DirectoryInfo>(),
            It.IsAny<CancellationToken>(),
            new string[] { "-m", "pip", "--version" }))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "pip 24.1.2 from C:\\Python312\\site-packages\\pip (python 3.12)" });

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var semVer = await service.GetPipVersionAsync();
        semVer.Major.Should().Be(24);
        semVer.Minor.Should().Be(1);
        semVer.Build.Should().Be(2);
    }

    [TestMethod]
    public async Task PipCommandService_BadVersionString_ReturnsNullAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DirectoryInfo>(),
            It.IsAny<CancellationToken>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "this is not a valid output" });

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var semVer = await service.GetPipVersionAsync();
        semVer.Should().BeNull();
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsVersionAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DirectoryInfo>(),
            It.IsAny<CancellationToken>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "pip 20.0.2 from c:\\python\\lib\\site-packages\\pip (python 3.8)" });

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var semVer = await service.GetPipVersionAsync();
        semVer.Major.Should().Be(20);
        semVer.Minor.Should().Be(0);
        semVer.Build.Should().Be(2);
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsVersion_SimpleAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DirectoryInfo>(),
            It.IsAny<CancellationToken>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "pip 24.0 from c:\\python\\lib\\site-packages\\pip (python 3.8)" });

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var semVer = await service.GetPipVersionAsync();
        semVer.Major.Should().Be(24);
        semVer.Minor.Should().Be(0);
    }

    [TestMethod]
    public async Task PipCommandService_ReturnsVersionForAPathAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(true);
        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            It.Is<string>(x => x == "pip" || x == "python"),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<DirectoryInfo>(),
            It.IsAny<CancellationToken>(),
            It.Is<string[]>(x => x.Last() == "--version")))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdOut = "pip 20.0.2 from c:\\python\\lib\\site-packages\\pip (python 3.8)" });

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var semVer = await service.GetPipVersionAsync("testPath");
        semVer.Major.Should().Be(20);
        semVer.Minor.Should().Be(0);
        semVer.Build.Should().Be(2);
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_RequirementsTxt_CorrectlyAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_single_pkg);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);

        ValidateRequirementsTxtReportFile(report, reportFile);

        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PythonPipCommandService_GeneratesReport_RequirementsTxt_CorrectlyAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(false);
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("python", It.IsAny<IEnumerable<string>>(), "-m", "pip", "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "python",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string[]>(s =>
                s.Any(e => e.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase))
                && s.Any(e => e.Equals("-m", StringComparison.OrdinalIgnoreCase)))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_single_pkg);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);

        ValidateRequirementsTxtReportFile(report, reportFile);

        this.commandLineInvokationService.Verify();
    }

    private static void ValidateRequirementsTxtReportFile(PipInstallationReport report, FileInfo reportFile)
    {
        // the file shouldn't exist since we're not writing to it in the test
        reportFile.Should().NotBeNull();
        reportFile.Exists.Should().Be(false);

        // validate report parameters
        report.Should().NotBeNull();
        report.Version.Should().Be("1");
        report.InstallItems.Should().NotBeNull();
        report.InstallItems.Should().ContainSingle();

        // validate packages
        report.InstallItems[0].Requested.Should().BeTrue();
        report.InstallItems[0].Metadata.Name.Should().Be("six");
        report.InstallItems[0].Metadata.Version.Should().Be("1.16.0");
        report.InstallItems[0].Metadata.License.Should().Be("MIT");
        report.InstallItems[0].Metadata.Author.Should().Be("Benjamin Peterson");
        report.InstallItems[0].Metadata.AuthorEmail.Should().Be("benjamin@python.org");
        report.InstallItems[0].Metadata.Maintainer.Should().BeNullOrEmpty();
        report.InstallItems[0].Metadata.MaintainerEmail.Should().BeNullOrEmpty();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_SetupPy_CorrectlyAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".py"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => s.Contains("-e .", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_single_pkg);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);

        // the file shouldn't exist since we're not writing to it in the test
        reportFile.Should().NotBeNull();
        reportFile.Exists.Should().Be(false);

        // validate report parameters
        report.Should().NotBeNull();
        report.Version.Should().Be("1");
        report.InstallItems.Should().NotBeNull();
        report.InstallItems.Should().ContainSingle();

        // validate packages
        report.InstallItems[0].Requested.Should().BeTrue();
        report.InstallItems[0].Metadata.Name.Should().Be("six");
        report.InstallItems[0].Metadata.Version.Should().Be("1.16.0");
        report.InstallItems[0].Metadata.License.Should().Be("MIT");
        report.InstallItems[0].Metadata.Author.Should().Be("Benjamin Peterson");
        report.InstallItems[0].Metadata.AuthorEmail.Should().Be("benjamin@python.org");
        report.InstallItems[0].Metadata.Maintainer.Should().BeNullOrEmpty();
        report.InstallItems[0].Metadata.MaintainerEmail.Should().BeNullOrEmpty();

        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_MultiRequirementsTxt_CorrectlyAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_multi_pkg);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);

        // the file shouldn't exist since we're not writing to it in the test
        reportFile.Should().NotBeNull();
        reportFile.Exists.Should().Be(false);

        // validate report parameters
        report.Should().NotBeNull();
        report.Version.Should().Be("1");
        report.InstallItems.Should().NotBeNull();
        report.InstallItems.Should().HaveCount(2);

        // validate packages
        report.InstallItems[0].Requested.Should().BeTrue();
        report.InstallItems[0].Metadata.Name.Should().Be("six");
        report.InstallItems[0].Metadata.Version.Should().Be("1.16.0");
        report.InstallItems[0].Metadata.License.Should().Be("MIT");
        report.InstallItems[0].Metadata.Author.Should().Be("Benjamin Peterson");
        report.InstallItems[0].Metadata.AuthorEmail.Should().Be("benjamin@python.org");
        report.InstallItems[0].Metadata.Maintainer.Should().BeNullOrEmpty();
        report.InstallItems[0].Metadata.MaintainerEmail.Should().BeNullOrEmpty();

        report.InstallItems[1].Requested.Should().BeTrue();
        report.InstallItems[1].Metadata.Name.Should().Be("python-dateutil");
        report.InstallItems[1].Metadata.Version.Should().Be("2.9.0.post0");
        report.InstallItems[1].Metadata.License.Should().Be("Dual License");
        report.InstallItems[1].Metadata.Author.Should().Be("Gustavo Niemeyer");
        report.InstallItems[1].Metadata.AuthorEmail.Should().Be("gustavo@niemeyer.net");
        report.InstallItems[1].Metadata.Maintainer.Should().Be("Paul Ganssle");
        report.InstallItems[1].Metadata.MaintainerEmail.Should().Be("dateutil@python.org");

        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_WithDuplicate_Async()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), "requirements.txt"));

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("PipReportIgnoreFileLevelIndexUrl")).Returns(true);
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        this.fileUtilityService.Setup(x => x.DuplicateFileWithoutLines(It.IsAny<string>(), "--index-url", "-i"))
            .Returns(("C:/asdf/temp.requirements.txt", true))
            .Verifiable();
        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_multi_pkg);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => s.Contains("temp.requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);
        this.fileUtilityService.Verify();
        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_WithoutDuplicate_Async()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), "requirements.txt"));

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("PipReportIgnoreFileLevelIndexUrl")).Returns(true);
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        this.fileUtilityService.Setup(x => x.DuplicateFileWithoutLines(It.IsAny<string>(), "--index-url", "-i"))
            .Returns((null, false))
            .Verifiable();
        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_multi_pkg);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => !s.Contains("temp.requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);
        this.fileUtilityService.Verify();
        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_UseFileIndex_Async()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), "requirements.txt"));

        this.envVarService.Setup(x => x.IsEnvironmentVariableValueTrue("PipReportIgnoreFileLevelIndexUrl")).Returns(false);
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_multi_pkg);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => !s.Contains("temp.requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);
        this.fileUtilityService.Verify(x => x.DuplicateFileWithoutLines(It.IsAny<string>(), "--index-url", "-i"), Times.Never);
        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_BadFile_FailsAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".randomfile"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(testPath);

        // the file shouldn't exist since we're not writing to it in the test
        reportFile.Should().BeNull();

        // validate report parameters
        report.Should().NotBeNull();
        report.Version.Should().BeNull();
        report.InstallItems.Should().BeNull();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_EmptyPath_FailsAsync()
    {
        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        var (report, reportFile) = await service.GenerateInstallationReportAsync(string.Empty);

        // the file shouldn't exist since we're not writing to it in the test
        reportFile.Should().BeNull();

        // validate report parameters
        report.Should().NotBeNull();
        report.Version.Should().BeNull();
        report.InstallItems.Should().BeNull();
    }

    [TestMethod]
    public async Task PipCommandService_GeneratesReport_RequirementsTxt_NonZeroExitAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 1, StdErr = "TestFail", StdOut = string.Empty })
            .Verifiable();

        var action = async () => await service.GenerateInstallationReportAsync(testPath, cancellationToken: CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();

        this.commandLineInvokationService.Verify();
    }

    [TestMethod]
    public async Task PipCommandService_CancelledAsync()
    {
        var testPath = Path.Join(Directory.GetCurrentDirectory(), string.Join(Guid.NewGuid().ToString(), ".txt"));

        this.commandLineInvokationService.Setup(x => x.CanCommandBeLocatedAsync("pip", It.IsAny<IEnumerable<string>>(), "--version")).ReturnsAsync(true);

        var service = new PipCommandService(
            this.commandLineInvokationService.Object,
            this.pathUtilityService,
            this.fileUtilityService.Object,
            this.envVarService.Object,
            this.logger.Object);

        this.commandLineInvokationService.Setup(x => x.ExecuteCommandAsync(
            "pip",
            It.IsAny<IEnumerable<string>>(),
            It.Is<DirectoryInfo>(d => d.FullName.Contains(Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>(),
            It.Is<string>(s => s.Contains("requirements.txt", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new CommandLineExecutionResult { ExitCode = -1, StdErr = string.Empty, StdOut = string.Empty })
            .Verifiable();

        this.fileUtilityService.Setup(x => x.ReadAllTextAsync(It.IsAny<FileInfo>()))
            .ReturnsAsync(TestResources.pip_report_single_pkg);

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var action = async () => await service.GenerateInstallationReportAsync(testPath, cancellationToken: cts.Token);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("PipReport: Cancelled*");
    }
}

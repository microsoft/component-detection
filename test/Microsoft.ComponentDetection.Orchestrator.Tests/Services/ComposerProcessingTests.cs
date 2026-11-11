#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Composer;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Experiments;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Spectre.Console;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComposerProcessingTests
{
    [TestMethod]
    [DataRow(false, false, false, false)]
    [DataRow(true, false, false, false)]
    [DataRow(false, true, false, false)]
    [DataRow(true, true, false, false)]
    [DataRow(false, true, true, false)]
    [DataRow(true, true, true, false)]
    [DataRow(false, false, false, true)]
    [DataRow(true, false, false, true)]
    [DataRow(false, true, false, true)]
    [DataRow(true, true, false, true)]
    [DataRow(false, true, true, true)]
    [DataRow(true, true, true, true)]
    public async Task ExperimentalComposer_AlwaysExecutesButOnlyIncludesResultsWhenEnabledAsync(bool enabled, bool manifestOnly, bool unresolved, bool platform)
    {
        var walker = new Mock<IObservableDirectoryWalkerFactory>();
        var location = Path.Combine(Path.GetTempPath(), manifestOnly ? "composer.json" : "composer.lock");
        var childLocation = Path.Combine(Path.GetTempPath(), "child", manifestOnly ? "composer.json" : "composer.lock");
        var manifest = unresolved
            ? """{"require":{"vendor/package":"^v1.0.0"}}"""
            : """{"require":{"vendor/package":"v1.0.0"}}""";
        var content = manifestOnly
            ? manifest
            : """{"packages":[{"name":"vendor/package","version":"v1.0.0"}]}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        using var childStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        walker.Setup(value => value.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IComponentRecorder>()))
            .Returns<DirectoryInfo, IEnumerable<string>, IComponentRecorder>((_, _, recorder) =>
            {
                var requests = new List<ProcessRequest>();
                (string Location, Stream Stream)[] files = [(location, stream), (childLocation, childStream)];
                foreach (var (fileLocation, fileStream) in files)
                {
                    var file = new Mock<IComponentStream>();
                    file.SetupGet(value => value.Location).Returns(fileLocation);
                    file.SetupGet(value => value.Stream).Returns(fileStream);
                    requests.Add(new ProcessRequest { ComponentStream = file.Object, SingleFileComponentRecorder = recorder.CreateSingleFileComponentRecorder(fileLocation) });
                }

                return requests.ToObservable();
            });
        var experiments = new Mock<IExperimentService>();
        var processor = new DetectorProcessingService(walker.Object, experiments.Object, NullLogger<DetectorProcessingService>.Instance, Mock.Of<IAnsiConsole>());
        var commands = new Mock<ICommandLineInvocationService>();
        commands.Setup(value => value.IsCommandLineExecution()).Returns(platform);
        commands.Setup(value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()))
            .ReturnsAsync((string _, IEnumerable<string> _, DirectoryInfo directory, CancellationToken _, string[] _) => new CommandLineExecutionResult
            {
                ExitCode = 0,
                StdOut = directory.FullName == Path.GetDirectoryName(location)
                    ? """{"platform":[{"name":"php","version":"8.4.27","direct-dependency":true},{"name":"ext-json","version":"8.4.27","direct-dependency":false}]}"""
                    : """{"platform":[{"name":"php","version":"8.3.15","direct-dependency":false},{"name":"ext-json","version":"8.4.27","direct-dependency":true}]}""",
            });
        var detector = new ComposerComponentDetector(Mock.Of<IComponentStreamEnumerableFactory>(), walker.Object, Mock.Of<IEnvironmentVariableService>(), Mock.Of<IFileUtilityService>(), commands.Object, NullLogger<ComposerComponentDetector>.Instance);
        var settings = new ScanSettings { SourceDirectory = new DirectoryInfo(Path.GetTempPath()), DetectorArgs = new Dictionary<string, string>(), NoSummary = true };
        var restrictions = new DetectorRestrictions { ExplicitlyEnabledDetectorIds = enabled ? ["Composer"] : [] };

        var expectedCount = (unresolved ? 0 : 1) + (platform ? 3 : 0);
        for (var scan = 0; scan < 2; scan++)
        {
            stream.Position = 0;
            childStream.Position = 0;
            var result = await processor.ProcessDetectorsAsync(settings, [detector], restrictions);

            result.ResultCode.Should().Be(ProcessingResultCode.Success);
            var components = result.ComponentRecorders.SelectMany(value => value.Recorder.GetDetectedComponents()).Select(value => value.Component).ToList();
            components.Should().HaveCount(enabled ? expectedCount : 0);
            if (enabled && !unresolved)
            {
                components.OfType<ComposerComponent>().Should().ContainSingle().Which.Version.Should().Be("v1.0.0");
                result.ComponentRecorders.Single().Recorder.GetDependencyGraphsByLocation()[location]
                    .IsDevelopmentDependency("vendor/package v1.0.0 - Composer").Should().BeFalse();
            }

            if (enabled && platform)
            {
                components.OfType<PhpSdkComponent>().Select(component => component.Id).Should()
                    .BeEquivalentTo(["php 8.4.27 - PhpSdk", "php 8.3.15 - PhpSdk", "ext-json 8.4.27 - PhpSdk"]);
                var graphs = result.ComponentRecorders.Single().Recorder.GetDependencyGraphsByLocation();
                graphs[location].GetAllExplicitlyReferencedComponents().Where(id => id.EndsWith(" - PhpSdk")).Should().Equal("php 8.4.27 - PhpSdk");
                graphs[childLocation].GetAllExplicitlyReferencedComponents().Where(id => id.EndsWith(" - PhpSdk")).Should().Equal("ext-json 8.4.27 - PhpSdk");
                graphs[location].IsDevelopmentDependency("php 8.4.27 - PhpSdk").Should().BeTrue();
                graphs[childLocation].IsDevelopmentDependency("php 8.3.15 - PhpSdk").Should().BeTrue();
                graphs[location].IsDevelopmentDependency("ext-json 8.4.27 - PhpSdk").Should().BeTrue();
                graphs[childLocation].IsDevelopmentDependency("ext-json 8.4.27 - PhpSdk").Should().BeTrue();
            }
        }

        experiments.Verify(
            value => value.RecordDetectorRun(
                detector,
                It.Is<ComponentRecorder>(recorder => recorder.GetDetectedComponents().Count() == expectedCount),
                settings,
                It.Is<DetectorRunResult>(run => run.ComponentsFoundCount == expectedCount && run.IsExperimental == !enabled)),
            Times.Exactly(2));
        foreach (var projectDirectory in new[] { Path.GetDirectoryName(location), Path.GetDirectoryName(childLocation) })
        {
            commands.Verify(
                value => value.ExecuteCommandAsync("composer", It.IsAny<IEnumerable<string>>(), It.Is<DirectoryInfo>(directory => directory.FullName == projectDirectory), It.IsAny<CancellationToken>(), It.IsAny<string[]>()),
                platform ? Times.Exactly(2) : Times.Never());
        }
    }
}

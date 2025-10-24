#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Experiments;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DetectorProcessingServiceTests
{
    private static readonly DirectoryInfo DefaultSourceDirectory = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "SomeSource", "Directory"));
    private static readonly ScanSettings DefaultArgs = new() { SourceDirectory = DefaultSourceDirectory, DetectorArgs = new Dictionary<string, string>() };

    private readonly Dictionary<string, DetectedComponent> componentDictionary = new Dictionary<string, DetectedComponent>()
    {
        { "firstFileDetectorId", new DetectedComponent(new NpmComponent($"{Guid.NewGuid()}", "FileComponentVersion1")) },
        { "secondFileDetectorId", new DetectedComponent(new NuGetComponent("FileComponentName2", "FileComponentVersion2")) },
        { "firstCommandDetectorId", new DetectedComponent(new NpmComponent("CommandComponentName1", "CommandComponentVersion1")) },
        { "secondCommandDetectorId",  new DetectedComponent(new NuGetComponent("CommandComponentName2", "CommandComponentVersion2")) },
        { "experimentalFileDetectorId", new DetectedComponent(new NuGetComponent("experimentalDetectorName", "experimentalDetectorVersion")) },
    };

    private readonly Mock<ILogger<DetectorProcessingService>> loggerMock;
    private readonly DetectorProcessingService serviceUnderTest;
    private readonly Mock<IObservableDirectoryWalkerFactory> directoryWalkerFactory;
    private readonly Mock<IExperimentService> experimentServiceMock;

    private readonly Mock<FileComponentDetector> firstFileComponentDetectorMock;
    private readonly Mock<FileComponentDetector> secondFileComponentDetectorMock;
    private readonly Mock<IComponentDetector> firstCommandComponentDetectorMock;
    private readonly Mock<IComponentDetector> secondCommandComponentDetectorMock;
    private readonly Mock<FileComponentDetector> experimentalFileComponentDetectorMock;

    private readonly bool isWin;

    private IEnumerable<IComponentDetector> detectorsToUse;

    public DetectorProcessingServiceTests()
    {
        this.experimentServiceMock = new Mock<IExperimentService>();
        this.loggerMock = new Mock<ILogger<DetectorProcessingService>>();
        this.directoryWalkerFactory = new Mock<IObservableDirectoryWalkerFactory>();
        this.serviceUnderTest =
            new DetectorProcessingService(this.directoryWalkerFactory.Object, this.experimentServiceMock.Object, this.loggerMock.Object);

        this.firstFileComponentDetectorMock = this.SetupFileDetectorMock("firstFileDetectorId");
        this.secondFileComponentDetectorMock = this.SetupFileDetectorMock("secondFileDetectorId");
        this.experimentalFileComponentDetectorMock = this.SetupFileDetectorMock("experimentalFileDetectorId");
        this.experimentalFileComponentDetectorMock.As<IExperimentalDetector>();

        this.firstCommandComponentDetectorMock = this.SetupCommandDetectorMock("firstCommandDetectorId");
        this.secondCommandComponentDetectorMock = this.SetupCommandDetectorMock("secondCommandDetectorId");

        this.isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    private IndividualDetectorScanResult ExpectedResultForDetector(string detectorId)
    {
        return new IndividualDetectorScanResult
        {
            AdditionalTelemetryDetails = new Dictionary<string, string> { { "detectorId", detectorId } },
            ResultCode = ProcessingResultCode.Success,
        };
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_HappyPathReturnsDetectedComponentsAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        var results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));

        this.ValidateExpectedComponents(results, this.detectorsToUse);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => x.Component?.Type == ComponentType.Npm).Component
            .Should().Be(this.componentDictionary[this.firstFileComponentDetectorMock.Object.Id].Component);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => x.Component?.Type == ComponentType.NuGet).Component
            .Should().Be(this.componentDictionary[this.secondFileComponentDetectorMock.Object.Id].Component);

        results.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_WithSourceFileLocationSetReturnsDetectedComponentsAsync()
    {
        var defaultArgs = new ScanSettings
        {
            SourceDirectory = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "SourceDirectory")),
            DetectorArgs = new Dictionary<string, string>(),
            SourceFileRoot = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "SourceDirectory", "SourceFileRoot")),
        };

        var componentDetectorMock1 = this.SetupFileDetectorMock("firstFileDetectorId", sourceDirectory: defaultArgs.SourceDirectory);
        var componentDetectorMock2 = this.SetupFileDetectorMock("secondFileDetectorId", sourceDirectory: defaultArgs.SourceDirectory);

        this.detectorsToUse =
        [
            componentDetectorMock1.Object, componentDetectorMock2.Object,
        ];

        var results = await this.serviceUnderTest.ProcessDetectorsAsync(defaultArgs, this.detectorsToUse, new DetectorRestrictions());

        componentDetectorMock1.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == defaultArgs.SourceDirectory && request.SourceFileRoot == defaultArgs.SourceFileRoot), It.IsAny<CancellationToken>()), Times.Once);
        componentDetectorMock2.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == defaultArgs.SourceDirectory && request.SourceFileRoot == defaultArgs.SourceFileRoot), It.IsAny<CancellationToken>()), Times.Once);

        this.ValidateExpectedComponents(results, this.detectorsToUse);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => x.Component?.Type == ComponentType.Npm).Component
            .Should().Be(this.componentDictionary[componentDetectorMock1.Object.Id].Component);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => x.Component?.Type == ComponentType.NuGet).Component
            .Should().Be(this.componentDictionary[componentDetectorMock2.Object.Id].Component);

        results.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_NullDetectedComponentsReturnIsCoalescedAsync()
    {
        var mockComponentDetector = new Mock<IComponentDetector>();
        mockComponentDetector.Setup(d => d.Id).Returns("test");

        mockComponentDetector.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                return new IndividualDetectorScanResult
                {
                    ResultCode = ProcessingResultCode.Success,
                    ContainerDetails = null,
                    AdditionalTelemetryDetails = null,
                };
            });

        this.detectorsToUse = [mockComponentDetector.Object];
        var results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        results.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_HappyPathReturns_DependencyGraphAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        var results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));

        foreach (var discoveredComponent in this.GetDiscoveredComponentsFromDetectorProcessingResult(results))
        {
            var componentId = discoveredComponent.Component.Id;
            var isMatched = false;
            foreach (var graph in results.ComponentRecorders.Select(componentRecorder => componentRecorder.Recorder.GetDependencyGraphsByLocation()).SelectMany(x => x.Values))
            {
                isMatched |= graph.GetComponents().Contains(componentId);
            }

            isMatched.Should().BeTrue();
        }
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_AdditionalTelemetryDetailsAreReturnedAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        var records = await TelemetryHelper.ExecuteWhileCapturingTelemetryAsync<DetectorExecutionTelemetryRecord>(async () =>
        {
            await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());
        });

        foreach (var record in records)
        {
            var additionalTelemetryDetails = JsonConvert.DeserializeObject<Dictionary<string, string>>(record.AdditionalTelemetryDetails);
            additionalTelemetryDetails["detectorId"].Should().Be(record.DetectorId);
        }
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_ExperimentalDetectorsDoNotReturnComponentsAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,

            this.experimentalFileComponentDetectorMock.Object,
        ];

        DetectorProcessingResult results = null;
        var records = await TelemetryHelper.ExecuteWhileCapturingTelemetryAsync<DetectorExecutionTelemetryRecord>(async () =>
        {
            results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());
        });

        var experimentalDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.experimentalFileComponentDetectorMock.Object.Id);
        var experimentalComponent = this.componentDictionary[this.experimentalFileComponentDetectorMock.Object.Id].Component as NuGetComponent;

        // protect against test code changes.
        experimentalComponent.Name.Should().NotBeNullOrEmpty("Experimental component should be nuget and have a name");

        experimentalDetectorRecord.Should().NotBeNull();
        experimentalDetectorRecord.DetectedComponentCount.Should().Be(1);
        experimentalDetectorRecord.IsExperimental.Should().BeTrue();

        // We should have all components except the ones that came from our experimental detector
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).Should().HaveCount(records.Sum(x => x.DetectedComponentCount ?? 0) - experimentalDetectorRecord.DetectedComponentCount ?? 0);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results)
            .Select(x => x.Component as NuGetComponent)
            .Where(x => x != null)
            .Should()
            .OnlyContain(x => x.Name != experimentalComponent.Name, "Experimental component should not be in component list");
        results.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.experimentalFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_ExperimentalDetectorsDoNormalStuffIfExplicitlyEnabledAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
            this.experimentalFileComponentDetectorMock.Object,
        ];

        var experimentalDetectorId = this.experimentalFileComponentDetectorMock.Object.Id;

        DetectorProcessingResult results = null;
        var records = await TelemetryHelper.ExecuteWhileCapturingTelemetryAsync<DetectorExecutionTelemetryRecord>(async () =>
        {
            results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions { ExplicitlyEnabledDetectorIds = [experimentalDetectorId] });
        });

        // We should have all components except the ones that came from our experimental detector
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).Should().HaveCount(records.Sum(x => x.DetectedComponentCount ?? 0));
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => (x.Component as NuGetComponent)?.Name == (this.componentDictionary[experimentalDetectorId].Component as NuGetComponent).Name)
            .Should().NotBeNull();
        results.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.experimentalFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_ExperimentalDetectorsThrowingDoesntKillDetectionAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
            this.experimentalFileComponentDetectorMock.Object,
        ];

        this.experimentalFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Simulated experimental failure"));

        DetectorProcessingResult results = null;
        var records = await TelemetryHelper.ExecuteWhileCapturingTelemetryAsync<DetectorExecutionTelemetryRecord>(async () =>
        {
            results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());
        });

        var experimentalDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.experimentalFileComponentDetectorMock.Object.Id);
        experimentalDetectorRecord.Should().NotBeNull();
        experimentalDetectorRecord.DetectedComponentCount.Should().Be(0);
        experimentalDetectorRecord.IsExperimental.Should().BeTrue();
        experimentalDetectorRecord.ReturnCode.Should().Be((int)ProcessingResultCode.InputError);
        experimentalDetectorRecord.ExperimentalInformation.Contains("Simulated experimental failure");

        // We should have all components except the ones that came from our experimental detector
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).Should().HaveCount(records.Sum(x => x.DetectedComponentCount ?? 0));
        results.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_DirectoryExclusionPredicateWorksAsExpectedAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object,
        ];

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Test is platform specific and fails on non-windows
            return;
        }

        var d1 = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "shouldExclude", "stuff"));
        var d2 = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "shouldNotExclude", "stuff"));

        ScanRequest capturedRequest = null;
        this.firstFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.ExpectedResultForDetector(this.firstFileComponentDetectorMock.Object.Id))
            .Callback<ScanRequest, CancellationToken>((request, token) => capturedRequest = request);

        await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Base case should match all directories.
        capturedRequest.DirectoryExclusionPredicate(DefaultArgs.SourceDirectory.Name, DefaultArgs.SourceDirectory.Parent.Name).Should().BeFalse();
        capturedRequest.DirectoryExclusionPredicate(d1.Name, d1.Parent.FullName).Should().BeFalse();

        var argsWithExclusion = new ScanSettings()
        {
            SourceDirectory = DefaultSourceDirectory,
            DetectorArgs = new Dictionary<string, string>(),
            DirectoryExclusionList = [Path.Combine("**", "SomeSource", "**"), Path.Combine("**", "shouldExclude", "**")],
        };

        // Now exercise the exclusion code
        await this.serviceUnderTest.ProcessDetectorsAsync(argsWithExclusion, this.detectorsToUse, new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Previous two tests should now exclude
        capturedRequest.DirectoryExclusionPredicate(DefaultArgs.SourceDirectory.Name, DefaultArgs.SourceDirectory.Parent.FullName).Should().BeTrue();
        capturedRequest.DirectoryExclusionPredicate(d1.Name, d1.Parent.FullName).Should().BeTrue();

        // Some other directory should still match
        capturedRequest.DirectoryExclusionPredicate(d2.Name, d2.Parent.FullName).Should().BeFalse();
    }

    [TestMethod]
    public void GenerateDirectoryExclusionPredicate_IgnoreCaseAndAllowWindowsPathsWorksAsExpected()
    {
        /*
         * We can't test a scenario like:
         *
         * SourceDirectory = /Some/Source/Directory
         * DirectoryExclusionList = *Some/*
         * allowWindowsPath = false
         *
         * and expect to exclude the directory, because when
         * we pass the SourceDirectory path to DirectoryInfo and we are running the test on Windows,
         * DirectoryInfo transalate it to C:\\Some\Source\Directory
         * making the test fail
         */

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Test is inconsistent for non-windows platforms
            return;
        }

        // This unit test previously depended on defaultArgs.   But the author assumed that \Source\ was in the default source path, which may not be true on all developers machines.
        // control this more explicitly.
        var args = new ScanSettings { SourceDirectory = new DirectoryInfo(this.isWin ? @"C:\Some\Source\Directory" : "/c/Some/Source/Directory"), DetectorArgs = new Dictionary<string, string>() };

        var dn = args.SourceDirectory.Name.AsSpan();
        var dp = args.SourceDirectory.Parent.FullName.AsSpan();

        // Exclusion predicate is case sensitive and allow windows path, the exclusion list follow the windows path structure and has a case mismatch with the directory path, should not exclude
        args.DirectoryExclusionList = [@"**\source\**"];
        var exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: false);
        exclusionPredicate(dn, dp).Should().BeFalse();

        // Exclusion predicate is case sensitive and allow windows path, the exclusion list follow the windows path structure and match directory path case, should exclude
        args.DirectoryExclusionList = [@"**\Source\**"];
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: false);
        exclusionPredicate(dn, dp).Should().BeTrue();

        // Exclusion predicate is not case sensitive and allow windows path, the exclusion list follow the windows path, should exclude
        args.DirectoryExclusionList = [@"**\sOuRce\**"];
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);
        exclusionPredicate(dn, dp).Should().BeTrue();

        // Exclusion predicate does not support windows path and the exclusion list define the path as a windows path, should not exclude
        args.DirectoryExclusionList = [@"**\Source\**"];
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: false, ignoreCase: true);
        exclusionPredicate(dn, dp).Should().BeFalse();

        // Exclusion predicate support windows path and the exclusion list define the path as a windows path, should exclude
        args.DirectoryExclusionList = [@"**\Source\**"];
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);
        exclusionPredicate(dn, dp).Should().BeTrue();

        // Exclusion predicate support windows path and the exclusion list does not define a windows path, should exclude
        args.DirectoryExclusionList = [@"**/Source/**", @"**/Source\**"];
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);
        exclusionPredicate(dn, dp).Should().BeTrue();
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_DirectoryExclusionPredicateWorksAsExpectedForObsoleteAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object,
        ];

        var sourceDirectory = DefaultSourceDirectory;
        var args = DefaultArgs;
        var d1 = new DirectoryInfo(Path.Combine(sourceDirectory.FullName, "Child"));
        var d2 = new DirectoryInfo(Path.Combine(sourceDirectory.FullName, "..", "bin"));
        var d3 = new DirectoryInfo(Path.Combine(sourceDirectory.FullName, "OtherChild"));

        foreach (var di in new[] { sourceDirectory, d1, d2, d3 })
        {
            if (!di.Exists)
            {
                di.Create();
            }
        }

        Environment.CurrentDirectory = sourceDirectory.FullName;

        ScanRequest capturedRequest = null;
        this.firstFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.ExpectedResultForDetector(this.firstFileComponentDetectorMock.Object.Id))
            .Callback<ScanRequest, CancellationToken>((request, token) => capturedRequest = request);

        await this.serviceUnderTest.ProcessDetectorsAsync(args, this.detectorsToUse, new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Base case should match all directories.
        capturedRequest.DirectoryExclusionPredicate(args.SourceDirectory.Name.AsSpan(), args.SourceDirectory.Parent.FullName.AsSpan()).Should().BeFalse();
        capturedRequest.DirectoryExclusionPredicate(d1.Name.AsSpan(), d1.Parent.FullName.AsSpan()).Should().BeFalse();

        // Now exercise the exclusion code
        args.DirectoryExclusionListObsolete = [Path.Combine("Child"), Path.Combine("..", "bin")];
        await this.serviceUnderTest.ProcessDetectorsAsync(
            args,
            [this.firstFileComponentDetectorMock.Object],
            new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Previous two tests should now exclude
        capturedRequest.DirectoryExclusionPredicate(d1.Name.AsSpan(), d1.Parent.FullName.AsSpan()).Should().BeTrue();
        capturedRequest.DirectoryExclusionPredicate(d2.Name.AsSpan(), d2.Parent.FullName.AsSpan()).Should().BeTrue();

        // Some other directory should still match
        capturedRequest.DirectoryExclusionPredicate(d3.Name.AsSpan(), d3.Parent.FullName.AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_CapturesTelemetryAsync()
    {
        var args = DefaultArgs;

        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        var records = await TelemetryHelper.ExecuteWhileCapturingTelemetryAsync<DetectorExecutionTelemetryRecord>(async () =>
        {
            await this.serviceUnderTest.ProcessDetectorsAsync(args, this.detectorsToUse, new DetectorRestrictions());
        });

        records.Should().Contain(x => x is DetectorExecutionTelemetryRecord);
        records.Count(x => x is not null)
            .Should().Be(2);
        var firstDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.firstFileComponentDetectorMock.Object.Id);
        firstDetectorRecord.Should().NotBeNull();
        firstDetectorRecord.ExecutionTime.Should().BePositive();

        var secondDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.secondFileComponentDetectorMock.Object.Id);
        secondDetectorRecord.Should().NotBeNull();

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_ExecutesMixedCommandAndFileDetectorsAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
            this.firstCommandComponentDetectorMock.Object,

            this.secondCommandComponentDetectorMock.Object,
        ];

        DetectorProcessingResult results = null;
        var records = await TelemetryHelper.ExecuteWhileCapturingTelemetryAsync<DetectorExecutionTelemetryRecord>(async () =>
        {
            results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());
        });

        results.Should().NotBeNull("Detector processing failed");

        records.Should().Contain(x => x is DetectorExecutionTelemetryRecord);

        records.Count(x => x is not null)
            .Should().Be(4);

        this.ValidateExpectedComponents(results, this.detectorsToUse);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.firstCommandComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
        this.secondCommandComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_FinishesExperimentsAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.experimentServiceMock.Verify(x => x.FinishAsync(), Times.Once());
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_RecordsDetectorRunsAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        var firstComponents = new[] { this.componentDictionary[this.firstFileComponentDetectorMock.Object.Id] };
        var secondComponents = new[] { this.componentDictionary[this.secondFileComponentDetectorMock.Object.Id] };

        await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.experimentServiceMock.Verify(
            x =>
                x.RecordDetectorRun(
                    It.Is<IComponentDetector>(detector => detector == this.firstFileComponentDetectorMock.Object),
                    It.IsAny<ComponentRecorder>(),
                    It.Is<ScanSettings>(x => x == DefaultArgs),
                    It.IsAny<DetectorRunResult>()),
            Times.Once());

        this.experimentServiceMock.Verify(
            x =>
                x.RecordDetectorRun(
                    It.Is<IComponentDetector>(detector => detector == this.secondFileComponentDetectorMock.Object),
                    It.IsAny<ComponentRecorder>(),
                    It.Is<ScanSettings>(x => x == DefaultArgs),
                    It.IsAny<DetectorRunResult>()),
            Times.Once());
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_InitializesExperimentsAsync()
    {
        this.detectorsToUse =
        [
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        ];

        await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.experimentServiceMock.Verify(x => x.InitializeAsync(), Times.Once);
    }

    private Mock<FileComponentDetector> SetupFileDetectorMock(string id, DirectoryInfo sourceDirectory = null)
    {
        var mockFileDetector = new Mock<FileComponentDetector>();
        mockFileDetector.SetupAllProperties();
        mockFileDetector.SetupGet(x => x.Id).Returns(id);

        sourceDirectory ??= DefaultArgs.SourceDirectory;
        this.componentDictionary.Should().ContainKey(id, $"MockDetector id:{id}, should be in mock dictionary");

        var expectedResult = this.ExpectedResultForDetector(id);

        mockFileDetector.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == sourceDirectory && request.ComponentRecorder != null), It.IsAny<CancellationToken>())).Returns(
            (ScanRequest request, CancellationToken cancellationToken) => mockFileDetector.Object.ExecuteDetectorAsync(request, cancellationToken)).Verifiable();

        mockFileDetector.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == sourceDirectory && request.ComponentRecorder != null), It.IsAny<CancellationToken>())).ReturnsAsync(
            (ScanRequest request, CancellationToken cancellationToken) =>
            {
                this.FillComponentRecorder(request.ComponentRecorder, id);
                return expectedResult;
            }).Verifiable();

        return mockFileDetector;
    }

    private IEnumerable<DetectedComponent> GetDiscoveredComponentsFromDetectorProcessingResult(DetectorProcessingResult detectorProcessingResult)
    {
        return detectorProcessingResult
            .ComponentRecorders
            .Select(componentRecorder => componentRecorder.Recorder.GetDetectedComponents())
            .SelectMany(x => x);
    }

    private void FillComponentRecorder(IComponentRecorder componentRecorder, string id)
    {
        var singleFileRecorder = componentRecorder.CreateSingleFileComponentRecorder("/mock/location");
        singleFileRecorder.RegisterUsage(this.componentDictionary[id], false);
    }

    private void ValidateExpectedComponents(DetectorProcessingResult result, IEnumerable<IComponentDetector> detectorsRan)
    {
        var shouldBePresent = detectorsRan.Where(detector => !(detector is IExperimentalDetector))
            .Select(detector => this.componentDictionary[detector.Id]);
        var isPresent = this.GetDiscoveredComponentsFromDetectorProcessingResult(result);

        var check = isPresent.Select(i => i.GetType());

        isPresent.All(discovered => shouldBePresent.Contains(discovered));
        shouldBePresent.Should().HaveSameCount(isPresent);
    }

    private Mock<IComponentDetector> SetupCommandDetectorMock(string id)
    {
        var mockCommandDetector = new Mock<IComponentDetector>();
        mockCommandDetector.SetupAllProperties();
        mockCommandDetector.SetupGet(x => x.Id).Returns(id);

        this.componentDictionary.Should().ContainKey(id, $"MockDetector id:{id}, should be in mock dictionary");

        mockCommandDetector.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory && !request.DetectorArgs.Any()), It.IsAny<CancellationToken>())).ReturnsAsync(
            (ScanRequest request, CancellationToken cancellationToken) =>
            {
                this.FillComponentRecorder(request.ComponentRecorder, id);
                return this.ExpectedResultForDetector(id);
            }).Verifiable();

        return mockCommandDetector;
    }
}

namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DetectorProcessingServiceTests
{
    private static readonly DirectoryInfo DefaultSourceDirectory = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "SomeSource", "Directory"));
    private static readonly BcdeArguments DefaultArgs = new BcdeArguments { SourceDirectory = DefaultSourceDirectory, DetectorArgs = Enumerable.Empty<string>() };

    private readonly Dictionary<string, DetectedComponent> componentDictionary = new Dictionary<string, DetectedComponent>()
    {
        { "firstFileDetectorId", new DetectedComponent(new NpmComponent($"{Guid.NewGuid()}", "FileComponentVersion1")) },
        { "secondFileDetectorId", new DetectedComponent(new NuGetComponent("FileComponentName2", "FileComponentVersion2")) },
        { "firstCommandDetectorId", new DetectedComponent(new NpmComponent("CommandComponentName1", "CommandComponentVersion1")) },
        { "secondCommandDetectorId",  new DetectedComponent(new NuGetComponent("CommandComponentName2", "CommandComponentVersion2")) },
        { "experimentalFileDetectorId", new DetectedComponent(new NuGetComponent("experimentalDetectorName", "experimentalDetectorVersion")) },
    };

    private IEnumerable<IComponentDetector> detectorsToUse;
    private Mock<ILogger> loggerMock;
    private DetectorProcessingService serviceUnderTest;
    private FastDirectoryWalkerFactory directoryWalkerFactory;

    private Mock<FileComponentDetector> firstFileComponentDetectorMock;
    private Mock<FileComponentDetector> secondFileComponentDetectorMock;
    private Mock<IComponentDetector> firstCommandComponentDetectorMock;
    private Mock<IComponentDetector> secondCommandComponentDetectorMock;
    private Mock<FileComponentDetector> experimentalFileComponentDetectorMock;

    private bool isWin;

    private IndividualDetectorScanResult ExpectedResultForDetector(string detectorId)
    {
        return new IndividualDetectorScanResult
        {
            AdditionalTelemetryDetails = new Dictionary<string, string> { { "detectorId", detectorId } },
            ResultCode = ProcessingResultCode.Success,
        };
    }

    [TestInitialize]
    public void TestInit()
    {
        this.loggerMock = new Mock<ILogger>();
        this.serviceUnderTest = new DetectorProcessingService
        {
            Logger = this.loggerMock.Object,
        };

        this.directoryWalkerFactory = new FastDirectoryWalkerFactory()
        {
            Logger = this.loggerMock.Object,
            PathUtilityService = new PathUtilityService(),
        };

        this.serviceUnderTest.Scanner = this.directoryWalkerFactory;

        this.firstFileComponentDetectorMock = this.SetupFileDetectorMock("firstFileDetectorId");
        this.secondFileComponentDetectorMock = this.SetupFileDetectorMock("secondFileDetectorId");
        this.experimentalFileComponentDetectorMock = this.SetupFileDetectorMock("experimentalFileDetectorId");
        this.experimentalFileComponentDetectorMock.As<IExperimentalDetector>();

        this.firstCommandComponentDetectorMock = this.SetupCommandDetectorMock("firstCommandDetectorId");
        this.secondCommandComponentDetectorMock = this.SetupCommandDetectorMock("secondCommandDetectorId");

        this.isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_HappyPathReturnsDetectedComponents()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        };

        var results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));

        this.ValidateExpectedComponents(results, this.detectorsToUse);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => x.Component?.Type == ComponentType.Npm).Component
            .Should().Be(this.componentDictionary[this.firstFileComponentDetectorMock.Object.Id].Component);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => x.Component?.Type == ComponentType.NuGet).Component
            .Should().Be(this.componentDictionary[this.secondFileComponentDetectorMock.Object.Id].Component);

        results.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_NullDetectedComponentsReturnIsCoalesced()
    {
        var mockComponentDetector = new Mock<IComponentDetector>();
        mockComponentDetector.Setup(d => d.Id).Returns("test");

        mockComponentDetector.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>()))
            .ReturnsAsync(() =>
            {
                return new IndividualDetectorScanResult
                {
                    ResultCode = ProcessingResultCode.Success,
                    ContainerDetails = null,
                    AdditionalTelemetryDetails = null,
                };
            });

        this.detectorsToUse = new[] { mockComponentDetector.Object };
        var results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        results.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_HappyPathReturns_DependencyGraph()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        };

        var results = await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));

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
    public void ProcessDetectorsAsync_AdditionalTelemetryDetailsAreReturned()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        };

        var records = TelemetryHelper.ExecuteWhileCapturingTelemetry<DetectorExecutionTelemetryRecord>(async () =>
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
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Need to Wait for Async lambda to execute.")]
    public void ProcessDetectorsAsync_ExperimentalDetectorsDoNotReturnComponents()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,

            this.experimentalFileComponentDetectorMock.Object,
        };

        DetectorProcessingResult results = null;
        var records = TelemetryHelper.ExecuteWhileCapturingTelemetry<DetectorExecutionTelemetryRecord>(() =>
        {
            results = this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions()).Result;
        });

        var experimentalDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.experimentalFileComponentDetectorMock.Object.Id);
        var experimentalComponent = this.componentDictionary[this.experimentalFileComponentDetectorMock.Object.Id].Component as NuGetComponent;

        // protect against test code changes.
        experimentalComponent.Name.Should().NotBeNullOrEmpty("Experimental component should be nuget and have a name");

        experimentalDetectorRecord.Should().NotBeNull();
        experimentalDetectorRecord.DetectedComponentCount.Should().Be(1);
        experimentalDetectorRecord.IsExperimental.Should().BeTrue();

        // We should have all components except the ones that came from our experimental detector
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).Count().Should().Be(records.Sum(x => x.DetectedComponentCount) - experimentalDetectorRecord.DetectedComponentCount);
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).All(x => (x.Component as NuGetComponent)?.Name != experimentalComponent.Name)
            .Should().BeTrue("Experimental component should not be in component list");
        results.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.experimentalFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
    }

    [TestMethod]
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Need to Wait for Async lambda to execute.")]
    public void ProcessDetectorsAsync_ExperimentalDetectorsDoNormalStuffIfExplicitlyEnabled()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
            this.experimentalFileComponentDetectorMock.Object,
        };

        var experimentalDetectorId = this.experimentalFileComponentDetectorMock.Object.Id;

        DetectorProcessingResult results = null;
        var records = TelemetryHelper.ExecuteWhileCapturingTelemetry<DetectorExecutionTelemetryRecord>(() =>
        {
            results = this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions { ExplicitlyEnabledDetectorIds = new[] { experimentalDetectorId } }).Result;
        });

        // We should have all components except the ones that came from our experimental detector
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).Count().Should().Be(records.Sum(x => x.DetectedComponentCount));
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).FirstOrDefault(x => (x.Component as NuGetComponent)?.Name == (this.componentDictionary[experimentalDetectorId].Component as NuGetComponent).Name)
            .Should().NotBeNull();
        results.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.experimentalFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
    }

    [TestMethod]
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Need to Wait for Async lambda to execute.")]
    public void ProcessDetectorsAsync_ExperimentalDetectorsThrowingDoesntKillDetection()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
            this.experimentalFileComponentDetectorMock.Object,
        };

        this.experimentalFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)))
            .Throws(new InvalidOperationException("Simulated experimental failure"));

        DetectorProcessingResult results = null;
        var records = TelemetryHelper.ExecuteWhileCapturingTelemetry<DetectorExecutionTelemetryRecord>(() =>
        {
            results = this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions()).Result;
        });

        var experimentalDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.experimentalFileComponentDetectorMock.Object.Id);
        experimentalDetectorRecord.Should().NotBeNull();
        experimentalDetectorRecord.DetectedComponentCount.Should().Be(0);
        experimentalDetectorRecord.IsExperimental.Should().BeTrue();
        experimentalDetectorRecord.ReturnCode.Should().Be((int)ProcessingResultCode.InputError);
        experimentalDetectorRecord.ExperimentalInformation.Contains("Simulated experimental failure");

        // We should have all components except the ones that came from our experimental detector
        this.GetDiscoveredComponentsFromDetectorProcessingResult(results).Count().Should().Be(records.Sum(x => x.DetectedComponentCount));
        results.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_DirectoryExclusionPredicateWorksAsExpected()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object,
        };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("Test is platform specific and fails on non-windows");
        }

        var d1 = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "shouldExclude", "stuff"));
        var d2 = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "shouldNotExclude", "stuff"));

        ScanRequest capturedRequest = null;
        this.firstFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>()))
            .ReturnsAsync(this.ExpectedResultForDetector(this.firstFileComponentDetectorMock.Object.Id))
            .Callback<ScanRequest>(request => capturedRequest = request);

        await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Base case should match all directories.
        capturedRequest.DirectoryExclusionPredicate(DefaultArgs.SourceDirectory.Name, DefaultArgs.SourceDirectory.Parent.Name).Should().BeFalse();
        capturedRequest.DirectoryExclusionPredicate(d1.Name, d1.Parent.FullName).Should().BeFalse();

        var argsWithExclusion = new BcdeArguments()
        {
            SourceDirectory = DefaultSourceDirectory,
            DetectorArgs = Enumerable.Empty<string>(),
            DirectoryExclusionList = new[] { Path.Combine("**", "SomeSource", "**"), Path.Combine("**", "shouldExclude", "**") },
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
            Assert.Inconclusive("Test is inconsistent for non-windows platforms");
        }

        // This unit test previously depended on defaultArgs.   But the author assumed that \Source\ was in the default source path, which may not be true on all developers machines.
        // control this more explicitly.
        var args = new BcdeArguments { SourceDirectory = new DirectoryInfo(this.isWin ? @"C:\Some\Source\Directory" : "/c/Some/Source/Directory"), DetectorArgs = Enumerable.Empty<string>() };

        var dn = args.SourceDirectory.Name.AsSpan();
        var dp = args.SourceDirectory.Parent.FullName.AsSpan();

        // Exclusion predicate is case sensitive and allow windows path, the exclusion list follow the windows path structure and has a case mismatch with the directory path, should not exclude
        args.DirectoryExclusionList = new[] { @"**\source\**" };
        var exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: false);
        Assert.IsFalse(exclusionPredicate(dn, dp));

        // Exclusion predicate is case sensitive and allow windows path, the exclusion list follow the windows path structure and match directory path case, should exclude
        args.DirectoryExclusionList = new[] { @"**\Source\**" };
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: false);
        Assert.IsTrue(exclusionPredicate(dn, dp));

        // Exclusion predicate is not case sensitive and allow windows path, the exclusion list follow the windows path, should exclude
        args.DirectoryExclusionList = new[] { @"**\sOuRce\**" };
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);
        Assert.IsTrue(exclusionPredicate(dn, dp));

        // Exclusion predicate does not support windows path and the exclusion list define the path as a windows path, should not exclude
        args.DirectoryExclusionList = new[] { @"**\Source\**" };
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: false, ignoreCase: true);
        Assert.IsFalse(exclusionPredicate(dn, dp));

        // Exclusion predicate support windows path and the exclusion list define the path as a windows path, should exclude
        args.DirectoryExclusionList = new[] { @"**\Source\**" };
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);
        Assert.IsTrue(exclusionPredicate(dn, dp));

        // Exclusion predicate support windows path and the exclusion list does not define a windows path, should exclude
        args.DirectoryExclusionList = new[] { @"**/Source/**", @"**/Source\**" };
        exclusionPredicate = this.serviceUnderTest.GenerateDirectoryExclusionPredicate(@"C:\somefake\dir", args.DirectoryExclusionList, args.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);
        Assert.IsTrue(exclusionPredicate(dn, dp));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_DirectoryExclusionPredicateWorksAsExpectedForObsolete()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object,
        };

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
        this.firstFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>()))
            .ReturnsAsync(this.ExpectedResultForDetector(this.firstFileComponentDetectorMock.Object.Id))
            .Callback<ScanRequest>(request => capturedRequest = request);

        await this.serviceUnderTest.ProcessDetectorsAsync(args, this.detectorsToUse, new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Base case should match all directories.
        capturedRequest.DirectoryExclusionPredicate(args.SourceDirectory.Name.AsSpan(), args.SourceDirectory.Parent.FullName.AsSpan()).Should().BeFalse();
        capturedRequest.DirectoryExclusionPredicate(d1.Name.AsSpan(), d1.Parent.FullName.AsSpan()).Should().BeFalse();

        // Now exercise the exclusion code
        args.DirectoryExclusionListObsolete = new[] { Path.Combine("Child"), Path.Combine("..", "bin") };
        await this.serviceUnderTest.ProcessDetectorsAsync(
            args,
            new[] { this.firstFileComponentDetectorMock.Object },
            new DetectorRestrictions());

        this.directoryWalkerFactory.Reset();

        // Previous two tests should now exclude
        capturedRequest.DirectoryExclusionPredicate(d1.Name.AsSpan(), d1.Parent.FullName.AsSpan()).Should().BeTrue();
        capturedRequest.DirectoryExclusionPredicate(d2.Name.AsSpan(), d2.Parent.FullName.AsSpan()).Should().BeTrue();

        // Some other directory should still match
        capturedRequest.DirectoryExclusionPredicate(d3.Name.AsSpan(), d3.Parent.FullName.AsSpan()).Should().BeFalse();
    }

    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Need to Wait for Async lambda to execute.")]
    [TestMethod]
    public void ProcessDetectorsAsync_CapturesTelemetry()
    {
        var args = DefaultArgs;

        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
        };

        var records = TelemetryHelper.ExecuteWhileCapturingTelemetry<DetectorExecutionTelemetryRecord>(() =>
        {
            this.serviceUnderTest.ProcessDetectorsAsync(args, this.detectorsToUse, new DetectorRestrictions()).Wait();
        });

        records.Should().Contain(x => x is DetectorExecutionTelemetryRecord);
        records.Count(x => x is not null)
            .Should().Be(2);
        var firstDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.firstFileComponentDetectorMock.Object.Id);
        firstDetectorRecord.Should().NotBeNull();
        firstDetectorRecord.ExecutionTime.Should().BePositive();

        var secondDetectorRecord = records.FirstOrDefault(x => x.DetectorId == this.secondFileComponentDetectorMock.Object.Id);
        secondDetectorRecord.Should().NotBeNull();

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
    }

    [TestMethod]
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Need to Wait for Async lambda to execute.")]
    public void ProcessDetectorsAsync_ExecutesMixedCommandAndFileDetectors()
    {
        this.detectorsToUse = new[]
        {
            this.firstFileComponentDetectorMock.Object, this.secondFileComponentDetectorMock.Object,
            this.firstCommandComponentDetectorMock.Object,

            this.secondCommandComponentDetectorMock.Object,
        };

        DetectorProcessingResult results = null;
        var records = TelemetryHelper.ExecuteWhileCapturingTelemetry<DetectorExecutionTelemetryRecord>(() =>
        {
            results = this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, this.detectorsToUse, new DetectorRestrictions()).Result;
        });

        results.Should().NotBeNull("Detector processing failed");

        records.Should().Contain(x => x is DetectorExecutionTelemetryRecord);

        records.Count(x => x is not null)
            .Should().Be(4);

        this.ValidateExpectedComponents(results, this.detectorsToUse);

        this.firstFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondFileComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.firstCommandComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
        this.secondCommandComponentDetectorMock.Verify(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory)));
    }

    [TestMethod]
    public async Task ProcessDetectorsAsync_HandlesDetectorArgs()
    {
        ScanRequest capturedRequest = null;
        this.firstFileComponentDetectorMock.Setup(x => x.ExecuteDetectorAsync(It.IsAny<ScanRequest>()))
            .ReturnsAsync(this.ExpectedResultForDetector(this.firstFileComponentDetectorMock.Object.Id))
            .Callback<ScanRequest>(request => capturedRequest = request);

        var args = DefaultArgs;
        args.DetectorArgs = new string[] { "arg1=val1", "arg2", "arg3=val3" };

        await this.serviceUnderTest.ProcessDetectorsAsync(DefaultArgs, new[] { this.firstFileComponentDetectorMock.Object }, new DetectorRestrictions());

        capturedRequest.DetectorArgs
            .Should().Contain("arg1", "val1")
            .And.NotContainKey("arg2")
            .And.Contain("arg3", "val3");
    }

    private Mock<FileComponentDetector> SetupFileDetectorMock(string id)
    {
        var mockFileDetector = new Mock<FileComponentDetector>();
        mockFileDetector.SetupAllProperties();
        mockFileDetector.SetupGet(x => x.Id).Returns(id);

        var sourceDirectory = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "Some", "Source", "Directory"));
        this.componentDictionary.Should().ContainKey(id, $"MockDetector id:{id}, should be in mock dictionary");

        var expectedResult = this.ExpectedResultForDetector(id);

        mockFileDetector.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory && request.ComponentRecorder != null))).Returns(
            (ScanRequest request) =>
            {
                return mockFileDetector.Object.ExecuteDetectorAsync(request);
            }).Verifiable();
        mockFileDetector.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory && request.ComponentRecorder != null))).ReturnsAsync(
            (ScanRequest request) =>
            {
                this.serviceUnderTest.Scanner.Initialize(request.SourceDirectory, request.DirectoryExclusionPredicate, 1);
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
        shouldBePresent.Should().HaveCount(isPresent.Count());
    }

    private Mock<IComponentDetector> SetupCommandDetectorMock(string id)
    {
        var mockCommandDetector = new Mock<IComponentDetector>();
        mockCommandDetector.SetupAllProperties();
        mockCommandDetector.SetupGet(x => x.Id).Returns(id);

        this.componentDictionary.Should().ContainKey(id, $"MockDetector id:{id}, should be in mock dictionary");

        mockCommandDetector.Setup(x => x.ExecuteDetectorAsync(It.Is<ScanRequest>(request => request.SourceDirectory == DefaultArgs.SourceDirectory && !request.DetectorArgs.Any()))).ReturnsAsync(
            (ScanRequest request) =>
            {
                this.FillComponentRecorder(request.ComponentRecorder, id);
                return this.ExpectedResultForDetector(id);
            }).Verifiable();

        return mockCommandDetector;
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class BcdeScanExecutionServiceTests
{
    private readonly Mock<ILogger<ScanExecutionService>> loggerMock;
    private readonly Mock<IDetectorProcessingService> detectorProcessingServiceMock;
    private readonly Mock<IEnumerable<IComponentDetector>> detectorsMock;
    private readonly Mock<IDetectorRestrictionService> detectorRestrictionServiceMock;
    private readonly Mock<IComponentDetector> componentDetector2Mock;
    private readonly Mock<IComponentDetector> componentDetector3Mock;
    private readonly Mock<IComponentDetector> versionedComponentDetector1Mock;
    private readonly IGraphTranslationService graphTranslationService;

    private readonly DetectedComponent[] detectedComponents;
    private readonly ContainerDetails sampleContainerDetails;

    private readonly ScanExecutionService serviceUnderTest;

    private readonly DirectoryInfo sourceDirectory;

    public BcdeScanExecutionServiceTests()
    {
        this.loggerMock = new Mock<ILogger<ScanExecutionService>>();
        this.detectorProcessingServiceMock = new Mock<IDetectorProcessingService>();
        this.detectorsMock = new Mock<IEnumerable<IComponentDetector>>();
        this.detectorRestrictionServiceMock = new Mock<IDetectorRestrictionService>();
        this.componentDetector2Mock = new Mock<IComponentDetector>();
        this.componentDetector3Mock = new Mock<IComponentDetector>();
        this.versionedComponentDetector1Mock = new Mock<IComponentDetector>();
        this.sampleContainerDetails = new ContainerDetails { Id = 1 };
        this.graphTranslationService = new DefaultGraphTranslationService(new Mock<ILogger<DefaultGraphTranslationService>>().Object);

        this.detectedComponents =
        [
            new DetectedComponent(new NpmComponent("some-npm-component", "1.2.3")),
            new DetectedComponent(new NuGetComponent("SomeNugetComponent", "1.2.3.4")),
        ];

        this.serviceUnderTest = new ScanExecutionService(
            this.detectorsMock.Object,
            this.detectorProcessingServiceMock.Object,
            this.detectorRestrictionServiceMock.Object,
            this.graphTranslationService,
            this.loggerMock.Object);

        this.sourceDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        this.sourceDirectory.Create();
    }

    [TestCleanup]
    public void CleanupTests()
    {
        this.detectorProcessingServiceMock.VerifyAll();
        this.detectorsMock.VerifyAll();
        this.detectorRestrictionServiceMock.VerifyAll();

        try
        {
            this.sourceDirectory.Delete(true);
        }
        catch
        {
        }
    }

    [TestMethod]
    public async Task DetectComponents_HappyPathAsync()
    {
        var componentRecorder = new ComponentRecorder(new Mock<ILogger>().Object);
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/some/file/path"));

        this.componentDetector2Mock.SetupGet(x => x.Id).Returns("Detector2");
        this.componentDetector2Mock.SetupGet(x => x.Version).Returns(1);
        this.componentDetector3Mock.SetupGet(x => x.Id).Returns("Detector3");
        this.componentDetector3Mock.SetupGet(x => x.Version).Returns(10);

        this.detectedComponents[0].DevelopmentDependency = true;
        this.detectedComponents[0].ContainerDetailIds =
        [
            this.sampleContainerDetails.Id,
        ];
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[0], isDevelopmentDependency: true);

        var parentPipComponent = new PipComponent("sample-root", "1.0");
        this.detectedComponents[1].DependencyRoots = new HashSet<TypedComponent>([parentPipComponent]);
        this.detectedComponents[1].DevelopmentDependency = null;
        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(parentPipComponent, detector: new Mock<IComponentDetector>().Object), isExplicitReferencedDependency: true);
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[1], parentComponentId: parentPipComponent.Id);

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };
        var result = await this.DetectComponentsHappyPathAsync(
            settings,
            restrictions =>
            {
                restrictions.AllowedDetectorCategories.Should().BeNull();
                restrictions.AllowedDetectorIds.Should().BeNull();
            },
            [componentRecorder]);

        result.Result.Should().Be(ProcessingResultCode.Success);
        this.ValidateDetectedComponents(result.DetectedComponents);
        result.DetectorsInRun.Should().HaveCount(2);
        var detector2 = result.DetectorsInRun.Single(x => x.DetectorId == "Detector2");
        detector2.Version.Should().Be(1);
        var detector3 = result.DetectorsInRun.Single(x => x.DetectorId == "Detector3");
        detector3.Version.Should().Be(10);

        var npmComponent = result.DetectedComponents.Single(x => x.Component is NpmComponent);
        npmComponent.LocationsFoundAt.Should().ContainSingle();
        npmComponent.LocationsFoundAt.First().Should().Be("/some/file/path");
        npmComponent.IsDevelopmentDependency.Should().Be(true);
        npmComponent.ContainerDetailIds.Contains(1).Should().Be(true);

        var nugetComponent = result.DetectedComponents.Single(x => x.Component is NuGetComponent);
        nugetComponent.TopLevelReferrers.Should().ContainSingle();
        (nugetComponent.TopLevelReferrers.First() as PipComponent).Name.Should().Be("sample-root");
        nugetComponent.IsDevelopmentDependency.Should().Be(null);
    }

    [TestMethod]
    public async Task DetectComponents_DetectOnlyWithIdAndCategoryRestrictionsAsync()
    {
        var settings = new ScanSettings
        {
            DetectorCategories = ["Category1", "Category2"],
            DetectorsFilter = ["Detector1", "Detector2"],
            SourceDirectory = this.sourceDirectory,
        };

        var componentRecorder = new ComponentRecorder();
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("/location");
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[0]);
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[1]);

        var result = await this.DetectComponentsHappyPathAsync(
            settings,
            restrictions =>
            {
                restrictions.AllowedDetectorCategories.Should().Contain(settings.DetectorCategories);
                restrictions.AllowedDetectorIds.Should().Contain(settings.DetectorsFilter);
            },
            [componentRecorder]);

        result.Result.Should().Be(ProcessingResultCode.Success);
        this.ValidateDetectedComponents(result.DetectedComponents);
    }

    [TestMethod]
    public async Task DetectComponents_DetectOnlyWithNoUrlAsync()
    {
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var componentRecorder = new ComponentRecorder();
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("/location");
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[0]);
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[1]);

        var result = await this.DetectComponentsHappyPathAsync(
            settings,
            restrictions =>
            {
            },
            [componentRecorder]);

        result.Result.Should().Be(ProcessingResultCode.Success);
        this.ValidateDetectedComponents(result.DetectedComponents);
    }

    [TestMethod]
    public async Task DetectComponents_ReturnsExperimentalDetectorInformationAsync()
    {
        this.componentDetector2Mock.As<IExperimentalDetector>();
        this.componentDetector3Mock.As<IExperimentalDetector>();

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var componentRecorder = new ComponentRecorder();
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("/location");
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[0]);
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[1]);

        var result = await this.DetectComponentsHappyPathAsync(settings, restrictions => { }, [componentRecorder]);

        result.Result.Should().Be(ProcessingResultCode.Success);
        this.ValidateDetectedComponents(result.DetectedComponents);
        result.DetectorsInRun.Should().OnlyContain(x => x.IsExperimental);
    }

    [TestMethod]
    public async Task DetectComponents_Graph_Happy_PathAsync()
    {
        var mockGraphLocation = "/some/dependency/graph";

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var componentRecorder = new ComponentRecorder();
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(mockGraphLocation);
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[0], isExplicitReferencedDependency: true, isDevelopmentDependency: true);
        singleFileComponentRecorder.RegisterUsage(this.detectedComponents[1], isDevelopmentDependency: false, parentComponentId: this.detectedComponents[0].Component.Id);

        var mockDependencyGraphA = new Mock<IDependencyGraph>();

        mockDependencyGraphA.Setup(x => x.GetComponents()).Returns(
        [
            this.detectedComponents[0].Component.Id, this.detectedComponents[1].Component.Id,
        ]);
        mockDependencyGraphA.Setup(x => x.GetDependenciesForComponent(this.detectedComponents[0].Component.Id))
            .Returns(
            [
                this.detectedComponents[1].Component.Id,
            ]);

        mockDependencyGraphA.Setup(x => x.IsComponentExplicitlyReferenced(this.detectedComponents[0].Component.Id)).Returns(true);
        mockDependencyGraphA.Setup(x => x.IsDevelopmentDependency(this.detectedComponents[0].Component.Id)).Returns(true);
        mockDependencyGraphA.Setup(x => x.IsDevelopmentDependency(this.detectedComponents[1].Component.Id)).Returns(false);

        var result = await this.DetectComponentsHappyPathAsync(settings, restrictions => { }, [componentRecorder]);

        result.SourceDirectory.Should().NotBeNull();
        result.SourceDirectory.Should().Be(this.sourceDirectory.ToString());

        result.Result.Should().Be(ProcessingResultCode.Success);
        result.DependencyGraphs.Should().ContainSingle();
        var matchingGraph = result.DependencyGraphs.First();
        matchingGraph.Key.Should().Be(mockGraphLocation);
        var explicitlyReferencedComponents = matchingGraph.Value.ExplicitlyReferencedComponentIds;
        explicitlyReferencedComponents.Should().ContainSingle().And.Contain(this.detectedComponents[0].Component.Id);

        var actualGraph = matchingGraph.Value.Graph;
        actualGraph.Keys.Should().HaveCount(2);
        actualGraph[this.detectedComponents[0].Component.Id].Should().ContainSingle();
        actualGraph[this.detectedComponents[0].Component.Id].Should().Contain(this.detectedComponents[1].Component.Id);
        actualGraph[this.detectedComponents[1].Component.Id].Should().BeNull();

        matchingGraph.Value.DevelopmentDependencies.Should().Contain(this.detectedComponents[0].Component.Id);
        matchingGraph.Value.DevelopmentDependencies.Should().NotContain(this.detectedComponents[1].Component.Id);

        matchingGraph.Value.Dependencies.Should().Contain(this.detectedComponents[1].Component.Id);
        matchingGraph.Value.Dependencies.Should().NotContain(this.detectedComponents[0].Component.Id);
    }

    [TestMethod]
    public async Task DetectComponents_Graph_AccumulatesGraphsOnSameLocationAsync()
    {
        var mockGraphLocation = "/some/dependency/graph";

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var componentRecorder = new ComponentRecorder();

        var mockDependencyGraphA = new Mock<IDependencyGraph>();

        mockDependencyGraphA.Setup(x => x.GetComponents()).Returns(
        [
            this.detectedComponents[0].Component.Id, this.detectedComponents[1].Component.Id,
        ]);
        mockDependencyGraphA.Setup(x => x.GetDependenciesForComponent(this.detectedComponents[0].Component.Id))
            .Returns(
            [
                this.detectedComponents[1].Component.Id,
            ]);

        mockDependencyGraphA.Setup(x => x.IsComponentExplicitlyReferenced(this.detectedComponents[0].Component.Id)).Returns(true);

        var singleFileComponentRecorderA = componentRecorder.CreateSingleFileComponentRecorder(mockGraphLocation);
        singleFileComponentRecorderA.RegisterUsage(this.detectedComponents[0], isExplicitReferencedDependency: true);
        singleFileComponentRecorderA.RegisterUsage(this.detectedComponents[1], parentComponentId: this.detectedComponents[0].Component.Id);

        var mockDependencyGraphB = new Mock<IDependencyGraph>();

        mockDependencyGraphB.Setup(x => x.GetComponents()).Returns(
        [
            this.detectedComponents[0].Component.Id, this.detectedComponents[1].Component.Id,
        ]);
        mockDependencyGraphB.Setup(x => x.GetDependenciesForComponent(this.detectedComponents[1].Component.Id))
            .Returns(
            [
                this.detectedComponents[0].Component.Id,
            ]);

        mockDependencyGraphB.Setup(x => x.IsComponentExplicitlyReferenced(this.detectedComponents[1].Component.Id)).Returns(true);

        var singleFileComponentRecorderB = componentRecorder.CreateSingleFileComponentRecorder(mockGraphLocation);
        singleFileComponentRecorderB.RegisterUsage(this.detectedComponents[1], isExplicitReferencedDependency: true);
        singleFileComponentRecorderB.RegisterUsage(this.detectedComponents[0], parentComponentId: this.detectedComponents[1].Component.Id);

        var result = await this.DetectComponentsHappyPathAsync(settings, restrictions => { }, [componentRecorder]);

        result.SourceDirectory.Should().NotBeNull();
        result.SourceDirectory.Should().Be(this.sourceDirectory.ToString());

        result.Result.Should().Be(ProcessingResultCode.Success);
        result.DependencyGraphs.Should().ContainSingle();
        var matchingGraph = result.DependencyGraphs.First();
        matchingGraph.Key.Should().Be(mockGraphLocation);
        var explicitlyReferencedComponents = matchingGraph.Value.ExplicitlyReferencedComponentIds;
        explicitlyReferencedComponents.Should().HaveCount(2);
        explicitlyReferencedComponents.Should().Contain(this.detectedComponents[0].Component.Id);
        explicitlyReferencedComponents.Should().Contain(this.detectedComponents[1].Component.Id);

        var actualGraph = matchingGraph.Value.Graph;
        actualGraph.Keys.Should().HaveCount(2);
        actualGraph[this.detectedComponents[0].Component.Id].Should().ContainSingle();
        actualGraph[this.detectedComponents[0].Component.Id].Should().Contain(this.detectedComponents[1].Component.Id);
        actualGraph[this.detectedComponents[1].Component.Id].Should().ContainSingle();
        actualGraph[this.detectedComponents[1].Component.Id].Should().Contain(this.detectedComponents[0].Component.Id);
    }

    [TestMethod]
    public async Task VerifyTranslation_ComponentsAreReturnedWithDevDependencyInfoAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);
        var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);
        var detectedComponent3 = new DetectedComponent(new NpmComponent("test", "3.0.0"), detector: npmDetector.Object);

        singleFileComponentRecorder.RegisterUsage(detectedComponent1, isDevelopmentDependency: true);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2, isDevelopmentDependency: false);
        singleFileComponentRecorder.RegisterUsage(detectedComponent3);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var detectedComponents = results.ComponentsFound;

        var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
        storedComponent1.IsDevelopmentDependency.Should().BeTrue();

        var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
        storedComponent2.IsDevelopmentDependency.Should().BeFalse();

        var storedComponent3 = detectedComponents.First(dc => dc.Component.Id == detectedComponent3.Component.Id);
        storedComponent3.IsDevelopmentDependency.Should().BeNull();
    }

    [TestMethod]
    public async Task VerifyTranslation_RootsFromMultipleLocationsAreAgregatedAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location1");
        var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);
        var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);

        singleFileComponentRecorder.RegisterUsage(detectedComponent1, isExplicitReferencedDependency: true);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2, parentComponentId: detectedComponent1.Component.Id);

        singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location2");
        var detectedComponent2NewLocation = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2NewLocation, isExplicitReferencedDependency: true);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var detectedComponents = results.ComponentsFound;

        var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
        storedComponent1.TopLevelReferrers.Should().ContainSingle();
        storedComponent1.TopLevelReferrers.Should().Contain(detectedComponent1.Component);

        var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
        storedComponent2.TopLevelReferrers.Should().HaveCount(2, "There 2 roots the component is root of itself in one location and other location the root is its parent");
        storedComponent2.TopLevelReferrers.Should().Contain(detectedComponent1.Component);
        storedComponent2.TopLevelReferrers.Should().Contain(detectedComponent2.Component);
    }

    [TestMethod]
    public async Task VerifyTranslation_AncesterFromMultipleLocationsAreAgregatedAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location1");
        var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);
        var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);

        singleFileComponentRecorder.RegisterUsage(detectedComponent1, isExplicitReferencedDependency: true);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2, parentComponentId: detectedComponent1.Component.Id);

        singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location2");
        var detectedComponent2NewLocation = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2NewLocation, isExplicitReferencedDependency: true);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var detectedComponents = results.ComponentsFound;

        var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
        storedComponent1.AncestralReferrers.Should().BeEmpty("If a component is a root, then no root of itself");

        var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
        storedComponent2.AncestralReferrers.Should().ContainSingle("There 1 roots, the component is root of other component");
        storedComponent2.AncestralReferrers.Should().Contain(detectedComponent1.Component);
    }

    public async Task VerifyTranslation_ComponentsAreReturnedWithRootsAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);
        var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);

        singleFileComponentRecorder.RegisterUsage(detectedComponent1, isExplicitReferencedDependency: true);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2, parentComponentId: detectedComponent1.Component.Id);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var detectedComponents = results.ComponentsFound;

        var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
        storedComponent1.TopLevelReferrers.Should().ContainSingle("If a component is a root, then is root of itself");
        storedComponent1.TopLevelReferrers.Should().Contain(detectedComponent1.Component);

        var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
        storedComponent2.TopLevelReferrers.Should().ContainSingle();
        storedComponent2.TopLevelReferrers.Should().Contain(detectedComponent1.Component);
    }

    [TestMethod]
    public async Task VerifyTranslation_ComponentsAreReturnedWithAncesterAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);
        var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector.Object);

        singleFileComponentRecorder.RegisterUsage(detectedComponent1, isExplicitReferencedDependency: true);
        singleFileComponentRecorder.RegisterUsage(detectedComponent2, parentComponentId: detectedComponent1.Component.Id);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var detectedComponents = results.ComponentsFound;

        var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
        storedComponent1.AncestralReferrers.Should().BeEmpty("If a component is a root, then no root of itself");

        var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
        storedComponent2.AncestralReferrers.Should().ContainSingle();
        storedComponent2.AncestralReferrers.Should().Contain(detectedComponent1.Component);
    }

    [TestMethod]
    public async Task VerifyTranslation_DevDependenciesAreMergedWhenSameComponentInDifferentFilesAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var firstRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileA");
        var secondRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileB");

        // These two merged should be true.
        var componentAWithNoDevDep = new DetectedComponent(new NpmComponent("testA", "1.0.0"), detector: npmDetector.Object);
        var componentAWithDevDepTrue = new DetectedComponent(new NpmComponent("testA", "1.0.0"), detector: npmDetector.Object);

        // These two merged should be false.
        var componentBWithNoDevDep = new DetectedComponent(new NpmComponent("testB", "1.0.0"), detector: npmDetector.Object);
        var componentBWithDevDepFalse = new DetectedComponent(new NpmComponent("testB", "1.0.0"), detector: npmDetector.Object);

        // These two merged should be false.
        var componentCWithDevDepTrue = new DetectedComponent(new NpmComponent("testC", "1.0.0"), detector: npmDetector.Object);
        var componentCWithDevDepFalse = new DetectedComponent(new NpmComponent("testC", "1.0.0"), detector: npmDetector.Object);

        // These two merged should be true.
        var componentDWithDevDepTrue = new DetectedComponent(new NpmComponent("testD", "1.0.0"), detector: npmDetector.Object);
        var componentDWithDevDepTrueCopy = new DetectedComponent(new NpmComponent("testD", "1.0.0"), detector: npmDetector.Object);

        // The hint for reading this test is to know that each "column" you see visually is what's being merged, so componentAWithNoDevDep is being merged "down" into componentAWithDevDepTrue.
        foreach ((var component, var isDevDep) in new[]
                 {
                     (componentAWithNoDevDep, null), (componentAWithDevDepTrue, true),
                     (componentBWithNoDevDep, (bool?)null), (componentBWithDevDepFalse, false),
                     (componentCWithDevDepTrue, true), (componentCWithDevDepFalse, false),
                     (componentDWithDevDepTrue, true), (componentDWithDevDepTrueCopy, true),
                 })
        {
            firstRecorder.RegisterUsage(component, isDevelopmentDependency: isDevDep);
        }

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var components = results.ComponentsFound;

        components.Single(x => ((NpmComponent)x.Component).Name == "testA").IsDevelopmentDependency.Should().Be(true);
        components.Single(x => ((NpmComponent)x.Component).Name == "testB").IsDevelopmentDependency.Should().Be(false);
        components.Single(x => ((NpmComponent)x.Component).Name == "testC").IsDevelopmentDependency.Should().Be(false);
        components.Single(x => ((NpmComponent)x.Component).Name == "testD").IsDevelopmentDependency.Should().Be(true);
    }

    [TestMethod]
    public async Task VerifyTranslation_LocationsAreMergedWhenSameComponentInDifferentFilesAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var firstRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/some/file/path"));
        firstRecorder.AddAdditionalRelatedFile(Path.Join(this.sourceDirectory.FullName, "/some/related/file/1"));
        var secondRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/some/other/file/path"));
        secondRecorder.AddAdditionalRelatedFile(Path.Join(this.sourceDirectory.FullName, "/some/related/file/2"));

        var firstComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);
        var secondComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);

        firstRecorder.RegisterUsage(firstComponent);
        secondRecorder.RegisterUsage(secondComponent);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var actualComponent = results.ComponentsFound.Single();

        actualComponent.LocationsFoundAt.Should().HaveCount(4);
        foreach (var path in new[]
                 {
                     "/some/file/path",
                     "/some/other/file/path",
                     "/some/related/file/1",
                     "/some/related/file/2",
                 })
        {
            actualComponent.LocationsFoundAt
                .FirstOrDefault(x => x == path)
                .Should().NotBeNull();
        }
    }

    [TestMethod]
    public async Task VerifyTranslation_RootsAreMergedWhenSameComponentInDifferentFilesAsync()
    {
        var componentRecorder = new ComponentRecorder();
        var npmDetector = new Mock<IComponentDetector>();
        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var firstRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileA");
        var secondRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileB");

        var root1 = new DetectedComponent(new NpmComponent("test1", "2.0.0"), detector: npmDetector.Object);
        var firstComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);

        var root2 = new DetectedComponent(new NpmComponent("test2", "3.0.0"), detector: npmDetector.Object);
        var secondComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);

        firstRecorder.RegisterUsage(root1, isExplicitReferencedDependency: true);
        firstRecorder.RegisterUsage(firstComponent, parentComponentId: root1.Component.Id);

        secondRecorder.RegisterUsage(root2, isExplicitReferencedDependency: true);
        secondRecorder.RegisterUsage(secondComponent, parentComponentId: root2.Component.Id);

        var results = await this.SetupRecorderBasedScanningAsync(settings, [componentRecorder]);

        var actualComponent = results.ComponentsFound.First(c => c.Component.Id == firstComponent.Component.Id);
        actualComponent.TopLevelReferrers.Should().HaveCount(2);
        actualComponent.TopLevelReferrers.OfType<NpmComponent>()
            .FirstOrDefault(x => x.Name == "test1" && x.Version == "2.0.0")
            .Should().NotBeNull();
        actualComponent.TopLevelReferrers.OfType<NpmComponent>()
            .FirstOrDefault(x => x.Name == "test2" && x.Version == "3.0.0")
            .Should().NotBeNull();
    }

    [TestMethod]
    public async Task VerifyTranslation_DetectedComponentExist_UpdateFunctionIsAppliedAsync()
    {
        var componentRecorder = new ComponentRecorder(new Mock<ILogger>().Object);
        var npmDetector = new Mock<IComponentDetector>();
        var args = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
        var detectedComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector.Object);

        singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: true);

        var results = await this.SetupRecorderBasedScanningAsync(args, [componentRecorder]);
        results.ComponentsFound.Single(component => component.Component.Id == detectedComponent.Component.Id).IsDevelopmentDependency.Should().BeTrue();

        singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: false);

        results = await this.SetupRecorderBasedScanningAsync(args, [componentRecorder]);
        results.ComponentsFound.Single(component => component.Component.Id == detectedComponent.Component.Id).IsDevelopmentDependency.Should().BeFalse();
    }

    [TestMethod]
    public async Task VerifyDisabledDetectorsisPopulatedAsync()
    {
        var componentRecorder = new ComponentRecorder(new Mock<ILogger>().Object);
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/some/file/path"));
        this.versionedComponentDetector1Mock.SetupGet(x => x.Id).Returns("Detector1");
        this.componentDetector2Mock.SetupGet(x => x.Id).Returns("Detector2");
        this.componentDetector3Mock.SetupGet(x => x.Id).Returns("Detector3");

        IEnumerable<IComponentDetector> registeredDetectors =
        [
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,

            this.versionedComponentDetector1Mock.Object,
        ];
        var restrictedDetectors = new[]
        {
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,
        };

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };
        var result = await this.DetectComponentsHappyPathAsync(
            settings,
            registeredDetectors,
            restrictedDetectors,
            restrictions =>
            {
                restrictions.AllowedDetectorCategories.Should().BeNull();
                restrictions.AllowedDetectorIds.Should().BeNull();
            },
            [componentRecorder]);

        result.DetectorsNotInRun.Should().ContainSingle();
        result.DetectorsNotInRun.Single(x => x.DetectorId == "Detector1");
    }

    [TestMethod]
    public async Task VerifyNoDisabledDetectorsisPopulatedAsync()
    {
        var componentRecorder = new ComponentRecorder(new Mock<ILogger>().Object);
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/some/file/path"));
        this.versionedComponentDetector1Mock.SetupGet(x => x.Id).Returns("Detector1");
        this.componentDetector2Mock.SetupGet(x => x.Id).Returns("Detector2");
        this.componentDetector3Mock.SetupGet(x => x.Id).Returns("Detector3");

        IEnumerable<IComponentDetector> registeredDetectors =
        [
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,
        ];
        var restrictedDetectors = new[]
        {
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,
        };

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };
        var result = await this.DetectComponentsHappyPathAsync(
            settings,
            registeredDetectors,
            restrictedDetectors,
            restrictions =>
            {
                restrictions.AllowedDetectorCategories.Should().BeNull();
                restrictions.AllowedDetectorIds.Should().BeNull();
            },
            [componentRecorder]);

        result.DetectorsNotInRun.Should().BeEmpty();
    }

    private async Task<TestOutput> DetectComponentsHappyPathAsync(
       ScanSettings settings,
       IEnumerable<IComponentDetector> registeredDetectors,
       IComponentDetector[] restrictedDetectors,
       Action<DetectorRestrictions> restrictionAsserter = null,
       IEnumerable<ComponentRecorder> componentRecorders = null)
    {
        this.detectorsMock.Setup(x => x.GetEnumerator())
            .Returns(registeredDetectors.GetEnumerator());
        this.detectorRestrictionServiceMock.Setup(
                x => x.ApplyRestrictions(
                    It.IsAny<DetectorRestrictions>(),
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => registeredDetectors.Intersect(inputDetectors).Count() == registeredDetectors.Count())))
            .Returns(restrictedDetectors)
            .Callback<DetectorRestrictions, IEnumerable<IComponentDetector>>(
                (restrictions, detectors) => restrictionAsserter?.Invoke(restrictions));

        // We initialize detected component's DetectedBy here because of a Moq constraint -- certain operations (Adding interfaces) have to happen before .Object
        this.detectedComponents[0].DetectedBy = this.componentDetector2Mock.Object;
        this.detectedComponents[1].DetectedBy = this.componentDetector3Mock.Object;

        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = new Dictionary<int, ContainerDetails>
            {
                {
                    this.sampleContainerDetails.Id, this.sampleContainerDetails
                },
            },
            ComponentRecorders = componentRecorders.Select(componentRecorder => (this.componentDetector2Mock.Object, componentRecorder)),
        };

        this.detectorProcessingServiceMock.Setup(x =>
                x.ProcessDetectorsAsync(
                    settings,
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => restrictedDetectors.Intersect(inputDetectors).Count() == restrictedDetectors.Length),
                    Match.Create<DetectorRestrictions>(restriction => true)))
            .ReturnsAsync(processingResult);

        var result = await this.serviceUnderTest.ExecuteScanAsync(settings);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        result.SourceDirectory.Should().NotBeNull();
        result.SourceDirectory.Should().Be(settings.SourceDirectory.ToString());

        var testOutput = new TestOutput((DefaultGraphScanResult)result);

        return testOutput;
    }

    private async Task<TestOutput> DetectComponentsHappyPathAsync(
        ScanSettings settings,
        Action<DetectorRestrictions> restrictionAsserter = null,
        IEnumerable<ComponentRecorder> componentRecorders = null)
    {
        IEnumerable<IComponentDetector> registeredDetectors =
        [
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,

            this.versionedComponentDetector1Mock.Object,
        ];
        var restrictedDetectors = new[]
        {
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,
        };

        this.detectorsMock.Setup(x => x.GetEnumerator())
            .Returns(registeredDetectors.GetEnumerator());
        this.detectorRestrictionServiceMock.Setup(
                x => x.ApplyRestrictions(
                    It.IsAny<DetectorRestrictions>(),
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => registeredDetectors.Intersect(inputDetectors).Count() == registeredDetectors.Count())))
            .Returns(restrictedDetectors)
            .Callback<DetectorRestrictions, IEnumerable<IComponentDetector>>(
                (restrictions, detectors) => restrictionAsserter?.Invoke(restrictions));

        // We initialize detected component's DetectedBy here because of a Moq constraint -- certain operations (Adding interfaces) have to happen before .Object
        this.detectedComponents[0].DetectedBy = this.componentDetector2Mock.Object;
        this.detectedComponents[1].DetectedBy = this.componentDetector3Mock.Object;

        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = new Dictionary<int, ContainerDetails>
            {
                {
                    this.sampleContainerDetails.Id, this.sampleContainerDetails
                },
            },
            ComponentRecorders = componentRecorders.Select(componentRecorder => (this.componentDetector2Mock.Object, componentRecorder)),
        };

        this.detectorProcessingServiceMock.Setup(x =>
                x.ProcessDetectorsAsync(
                    settings,
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => restrictedDetectors.Intersect(inputDetectors).Count() == restrictedDetectors.Length),
                    Match.Create<DetectorRestrictions>(restriction => true)))
            .ReturnsAsync(processingResult);

        var result = await this.serviceUnderTest.ExecuteScanAsync(settings);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        result.SourceDirectory.Should().NotBeNull();
        result.SourceDirectory.Should().Be(settings.SourceDirectory.ToString());

        var testOutput = new TestOutput((DefaultGraphScanResult)result);

        return testOutput;
    }

    private async Task<ScanResult> SetupRecorderBasedScanningAsync(
        ScanSettings settings,
        IEnumerable<ComponentRecorder> componentRecorders)
    {
        IEnumerable<IComponentDetector> registeredDetectors =
        [
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,

            this.versionedComponentDetector1Mock.Object,
        ];
        var restrictedDetectors = new[]
        {
            this.componentDetector2Mock.Object, this.componentDetector3Mock.Object,
        };

        this.detectorsMock.Setup(x => x.GetEnumerator())
            .Returns(registeredDetectors.GetEnumerator());
        this.detectorRestrictionServiceMock.Setup(
                x => x.ApplyRestrictions(
                    It.IsAny<DetectorRestrictions>(),
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => registeredDetectors.Intersect(inputDetectors).Count() == registeredDetectors.Count())))
            .Returns(restrictedDetectors);

        // We initialize detected component's DetectedBy here because of a Moq constraint -- certain operations (Adding interfaces) have to happen before .Object
        this.detectedComponents[0].DetectedBy = this.componentDetector2Mock.Object;
        this.detectedComponents[1].DetectedBy = this.componentDetector3Mock.Object;

        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = new Dictionary<int, ContainerDetails>
            {
                {
                    this.sampleContainerDetails.Id, this.sampleContainerDetails
                },
            },
            ComponentRecorders = componentRecorders.Select(componentRecorder => (this.componentDetector2Mock.Object, componentRecorder)),
        };

        this.detectorProcessingServiceMock.Setup(x =>
                x.ProcessDetectorsAsync(
                    settings,
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => restrictedDetectors.Intersect(inputDetectors).Count() == restrictedDetectors.Length),
                    Match.Create<DetectorRestrictions>(restriction => true)))
            .ReturnsAsync(processingResult);

        var result = await this.serviceUnderTest.ExecuteScanAsync(settings);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        result.SourceDirectory.Should().NotBeNull();
        result.SourceDirectory.Should().Be(settings.SourceDirectory.ToString());

        return result;
    }

    private void ValidateDetectedComponents(IEnumerable<ScannedComponent> scannedComponents)
    {
        var npmComponent = scannedComponents.Where(x => x.Component.Type == ComponentType.Npm).Select(x => x.Component as NpmComponent).FirstOrDefault();
        npmComponent.Should().NotBeNull();
        npmComponent.Name.Should().Be(((NpmComponent)this.detectedComponents[0].Component).Name);
        var nugetComponent = scannedComponents.Where(x => x.Component.Type == ComponentType.NuGet).Select(x => x.Component as NuGetComponent).FirstOrDefault();
        nugetComponent.Should().NotBeNull();
        nugetComponent.Name.Should().Be(((NuGetComponent)this.detectedComponents[1].Component).Name);
    }

    private class TestOutput
    {
        public TestOutput(DefaultGraphScanResult result)
        {
            this.Result = result.ResultCode;
            this.DetectedComponents = result.ComponentsFound;
            this.DetectorsInRun = result.DetectorsInScan;
            this.DetectorsNotInRun = result.DetectorsNotInScan;
            this.DependencyGraphs = result.DependencyGraphs;
            this.SourceDirectory = result.SourceDirectory;
        }

        internal ProcessingResultCode Result { get; set; }

        internal IEnumerable<ScannedComponent> DetectedComponents { get; set; }

        internal IEnumerable<Detector> DetectorsInRun { get; set; }

        internal IEnumerable<Detector> DetectorsNotInRun { get; set; }

        internal DependencyGraphCollection DependencyGraphs { get; set; }

        internal string SourceDirectory { get; set; }
    }
}

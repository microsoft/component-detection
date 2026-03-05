#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
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
public class DefaultGraphTranslationServiceTests
{
    private readonly DefaultGraphTranslationService serviceUnderTest;
    private readonly ContainerDetails sampleContainerDetails;
    private readonly ComponentRecorder componentRecorder;
    private readonly Mock<IComponentDetector> componentDetectorMock;
    private readonly DirectoryInfo sourceDirectory;

    public DefaultGraphTranslationServiceTests()
    {
        this.serviceUnderTest = new DefaultGraphTranslationService(new Mock<ILogger<DefaultGraphTranslationService>>().Object);
        this.componentRecorder = new ComponentRecorder(new Mock<ILogger>().Object);

        this.sampleContainerDetails = new ContainerDetails { Id = 1 };
        this.componentDetectorMock = new Mock<IComponentDetector>();
        this.componentDetectorMock.SetupGet(x => x.Id).Returns("Detector1");
        this.componentDetectorMock.SetupGet(x => x.Version).Returns(1);
        this.sourceDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        this.sourceDirectory.Create();
    }

    [TestMethod]
    public void GenerateScanResultFromResult_MergesLicensesConcluded()
    {
        var singleFileRecorder1 = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/file1"));
        var singleFileRecorder2 = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/file2"));

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT"],
        };

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT", "Apache-2.0"],
        };

        singleFileRecorder1.RegisterUsage(component1);
        singleFileRecorder2.RegisterUsage(component2);

        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = [],
            ComponentRecorders = [(this.componentDetectorMock.Object, this.componentRecorder)],
        };

        var result = this.serviceUnderTest.GenerateScanResultFromProcessingResult(
            processingResult, new ScanSettings { SourceDirectory = this.sourceDirectory });

        var merged = result.ComponentsFound.Single();
        merged.LicensesConcluded.Should().BeEquivalentTo(["MIT", "Apache-2.0"]);
    }

    [TestMethod]
    public void GenerateScanResultFromResult_MergesSuppliers()
    {
        var singleFileRecorder1 = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/file1"));
        var singleFileRecorder2 = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/file2"));

        var supplier1 = new ActorInfo { Name = "Contoso", Type = "Organization" };
        var supplier2 = new ActorInfo { Name = "contoso", Type = "organization" };
        var supplier3 = new ActorInfo { Name = "Fabrikam", Type = "Organization" };

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            Suppliers = [supplier1],
        };

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            Suppliers = [supplier2, supplier3],
        };

        singleFileRecorder1.RegisterUsage(component1);
        singleFileRecorder2.RegisterUsage(component2);

        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = [],
            ComponentRecorders = [(this.componentDetectorMock.Object, this.componentRecorder)],
        };

        var result = this.serviceUnderTest.GenerateScanResultFromProcessingResult(
            processingResult, new ScanSettings { SourceDirectory = this.sourceDirectory });

        var merged = result.ComponentsFound.Single();

        // supplier1 and supplier2 are case-insensitively equal, so should be deduped
        merged.Suppliers.Should().HaveCount(2);
        merged.Suppliers.Should().Contain(s => string.Equals(s.Name, "Contoso", System.StringComparison.OrdinalIgnoreCase));
        merged.Suppliers.Should().Contain(s => s.Name == "Fabrikam");
    }

    [TestMethod]
    public void GenerateScanResultFromResult_NullLicensesConcluded_PreservesNonNull()
    {
        var singleFileRecorder1 = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/file1"));
        var singleFileRecorder2 = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, "/file2"));

        var component1 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = null,
        };

        var component2 = new DetectedComponent(new NpmComponent("pkg", "1.0.0"))
        {
            LicensesConcluded = ["MIT"],
        };

        singleFileRecorder1.RegisterUsage(component1);
        singleFileRecorder2.RegisterUsage(component2);

        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = [],
            ComponentRecorders = [(this.componentDetectorMock.Object, this.componentRecorder)],
        };

        var result = this.serviceUnderTest.GenerateScanResultFromProcessingResult(
            processingResult, new ScanSettings { SourceDirectory = this.sourceDirectory });

        var merged = result.ComponentsFound.Single();
        merged.LicensesConcluded.Should().BeEquivalentTo(["MIT"]);
    }

    [TestMethod]
    public void GenerateScanResultFromResult_WithCustomLocations()
    {
        var detectedFilePath = "/some/file/path";
        var npmCustomPath = "/custom/path.js";
        var nugetCustomPath = "/custom/path2.csproj";
        var relatedFilePath = "/generic/relevant/path";

        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, detectedFilePath));
        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = new Dictionary<int, ContainerDetails>
            {
                {
                    this.sampleContainerDetails.Id, this.sampleContainerDetails
                },
            },
            ComponentRecorders = [(this.componentDetectorMock.Object, this.componentRecorder)],
        };

        var expectedNpmComponent = new NpmComponent("npm-component", "1.2.3");
        var expectedNugetComponent = new NuGetComponent("nugetComponent", "4.5.6");
        var detectedNpmComponent = new DetectedComponent(expectedNpmComponent);
        var detectedNugetComponent = new DetectedComponent(expectedNugetComponent);

        // Any Related File will be reported for ALL components found in this graph
        singleFileComponentRecorder.AddAdditionalRelatedFile(Path.Join(this.sourceDirectory.FullName, relatedFilePath));

        // Registering components in same manifest with different custom paths
        detectedNpmComponent.AddComponentFilePath(Path.Join(this.sourceDirectory.FullName, npmCustomPath));
        detectedNugetComponent.AddComponentFilePath(Path.Join(this.sourceDirectory.FullName, nugetCustomPath));

        singleFileComponentRecorder.RegisterUsage(detectedNpmComponent, isDevelopmentDependency: false);
        singleFileComponentRecorder.RegisterUsage(detectedNugetComponent, isDevelopmentDependency: true);

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        var result = this.serviceUnderTest.GenerateScanResultFromProcessingResult(processingResult, settings);
        result.Should().NotBeNull();
        result.ComponentsFound.Should().HaveCount(2);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var resultNpmComponent = result.ComponentsFound.Single(c => c.Component.Type == ComponentType.Npm);
        var resultNugetComponent = result.ComponentsFound.Single(c => c.Component.Type == ComponentType.NuGet);

        resultNpmComponent.LocationsFoundAt.Should().BeEquivalentTo([npmCustomPath, detectedFilePath, relatedFilePath]);
        resultNugetComponent.LocationsFoundAt.Should().BeEquivalentTo([nugetCustomPath, detectedFilePath, relatedFilePath]);

        var actualNpmComponent = resultNpmComponent.Component as NpmComponent;
        var actualNugetComponent = resultNugetComponent.Component as NuGetComponent;

        actualNpmComponent.Should().BeEquivalentTo(expectedNpmComponent);
        actualNugetComponent.Should().BeEquivalentTo(expectedNugetComponent);
    }

    [TestMethod]
    public void GenerateScanResultFromResult_WithCustomLocations_WithExperimentsDryRun()
    {
        var detectedFilePath = "/some/file/path";
        var npmCustomPath = "/custom/path.js";
        var npmCustomPath2 = "D:/dummy/engtools/packages.lock.json";
        var nugetCustomPath = "/custom/path2.csproj";
        var relatedFilePath = "/generic/relevant/path";

        var singleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(Path.Join(this.sourceDirectory.FullName, detectedFilePath));
        var processingResult = new DetectorProcessingResult
        {
            ResultCode = ProcessingResultCode.Success,
            ContainersDetailsMap = new Dictionary<int, ContainerDetails>
            {
                {
                    this.sampleContainerDetails.Id, this.sampleContainerDetails
                },
            },
            ComponentRecorders = [(this.componentDetectorMock.Object, this.componentRecorder)],
        };

        var expectedNpmComponent = new NpmComponent("npm-component", "1.2.3");
        var expectedNugetComponent = new NuGetComponent("nugetComponent", "4.5.6");
        var detectedNpmComponent = new DetectedComponent(expectedNpmComponent);
        var detectedNugetComponent = new DetectedComponent(expectedNugetComponent);

        // Any Related File will be reported for ALL components found in this graph
        singleFileComponentRecorder.AddAdditionalRelatedFile(Path.Join(this.sourceDirectory.FullName, relatedFilePath));

        // Registering components in same manifest with different custom paths
        detectedNpmComponent.AddComponentFilePath(Path.Join(this.sourceDirectory.FullName, npmCustomPath));
        detectedNpmComponent.AddComponentFilePath(npmCustomPath2);
        detectedNugetComponent.AddComponentFilePath(Path.Join(this.sourceDirectory.FullName, nugetCustomPath));

        singleFileComponentRecorder.RegisterUsage(detectedNpmComponent, isDevelopmentDependency: false);
        singleFileComponentRecorder.RegisterUsage(detectedNugetComponent, isDevelopmentDependency: true);

        var settings = new ScanSettings
        {
            SourceDirectory = this.sourceDirectory,
        };

        // Experiments tool generates the graph but should not update the locations at all.
        var result = this.serviceUnderTest.GenerateScanResultFromProcessingResult(processingResult, settings, updateLocations: false);
        result.Should().NotBeNull();
        result.ComponentsFound.Should().HaveCount(2);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // next run will record the actual locations from non-experimental detectors
        result = this.serviceUnderTest.GenerateScanResultFromProcessingResult(processingResult, settings);
        result.Should().NotBeNull();
        result.ComponentsFound.Should().HaveCount(2);
        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        var resultNpmComponent = result.ComponentsFound.Single(c => c.Component.Type == ComponentType.Npm);
        var resultNugetComponent = result.ComponentsFound.Single(c => c.Component.Type == ComponentType.NuGet);

        // for now there is a bug that adds a forward slash to the path for symbolic links. This will be fixed in a future PR if we can parse dependency graph better for those.
        resultNpmComponent.LocationsFoundAt.Should().BeEquivalentTo([npmCustomPath, detectedFilePath, relatedFilePath, $"/{npmCustomPath2}"]);
        resultNugetComponent.LocationsFoundAt.Should().BeEquivalentTo([nugetCustomPath, detectedFilePath, relatedFilePath]);

        var actualNpmComponent = resultNpmComponent.Component as NpmComponent;
        var actualNugetComponent = resultNugetComponent.Component as NuGetComponent;

        actualNpmComponent.Should().BeEquivalentTo(expectedNpmComponent);
        actualNugetComponent.Should().BeEquivalentTo(expectedNugetComponent);
    }
}

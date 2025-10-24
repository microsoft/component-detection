#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Tests.Mocks;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetProjectModelProjectCentricComponentDetectorTests : BaseDetectorTest<NuGetProjectModelProjectCentricComponentDetector>
{
    private readonly string projectAssetsJsonFileName = "project.assets.json";
    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;

    public NuGetProjectModelProjectCentricComponentDetectorTests()
    {
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>()))
            .Returns(true);
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_Base_2_2_VerificationAsync()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(22);

        var nonDevComponents = detectedComponents.Where(c => !componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        nonDevComponents.Should().HaveCount(3);

        foreach (var component in detectedComponents)
        {
            component.TargetFrameworks.Should().BeEquivalentTo(["netcoreapp2.2"]);
        }

        detectedComponents.Select(x => x.Component).Cast<NuGetComponent>().FirstOrDefault(x => x.Name.Contains("coverlet.msbuild")).Should().NotBeNull();

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.Should().Contain(location => location.Contains("Loader.csproj")));
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_Base_2_2_additional_VerificationAsync()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2_additional);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(68);

        var nonDevComponents = detectedComponents.Where(c => !componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        nonDevComponents.Should().HaveCount(25);
        nonDevComponents.Select(x => x.Component).Cast<NuGetComponent>().FirstOrDefault(x => x.Name.Contains("Polly")).Should().NotBeNull();
        nonDevComponents.Select(x => x.Component).Cast<NuGetComponent>().Count(x => x.Name.Contains("System.Composition")).Should().Be(5);

        var nugetVersioning = nonDevComponents.FirstOrDefault(x => (x.Component as NuGetComponent).Name.Contains("NuGet.DependencyResolver.Core"));
        nugetVersioning.Should().NotBeNull();

        componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NuGetComponent>(
            nugetVersioning.Component.Id,
            x => x.Name.Contains("NuGet.ProjectModel")).Should().BeTrue();

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.Should().Contain(location => location.Contains("Detectors.csproj")));
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_2_2_VerificationAsync()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var dependencies = componentRecorder.GetDetectedComponents();
        var developmentDependencies = dependencies.Where(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        developmentDependencies.Should().HaveCount(19);
        developmentDependencies.Should().Contain(c => c.Component.Id.StartsWith("Microsoft.NETCore.Platforms "), "Microsoft.NETCore.Platforms should be treated as a development dependency.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_DependencyGraph_2_2_additional_VerificationAsync()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2_additional);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();
        var graphsByLocation = componentRecorder.GetDependencyGraphsByLocation();
        var graph = graphsByLocation.Values.First();

        var expectedDependencyIdsForCompositionTypedParts = new[]
        {
            "NuGet.DependencyResolver.Core 5.6.0 - NuGet",
        };

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var componentDetectionCommon = detectedComponents.First(x => x.Component.Id.Contains("NuGet.ProjectModel"));
        var dependencies = graph.GetDependenciesForComponent(componentDetectionCommon.Component.Id);
        foreach (var expectedId in expectedDependencyIdsForCompositionTypedParts)
        {
            dependencies.Should().Contain(expectedId);
        }

        expectedDependencyIdsForCompositionTypedParts.Should().HaveSameCount(dependencies);

        detectedComponents.Should().HaveSameCount(graph.GetComponents());

        // Top level dependencies look like this:
        // (we expect all non-proj and non-framework to show up as explicit refs, so those will be absent from the check)
        //
        // "DotNet.Glob >= 2.1.1",
        // "Microsoft.NETCore.App >= 2.2.8",
        // "Microsoft.VisualStudio.Services.Governance.ComponentDetection.Common >= 1.0.0",
        // "Microsoft.VisualStudio.Services.Governance.ComponentDetection.Contracts >= 1.0.0",
        // "MinVer >= 2.5.0",
        // "Nett >= 0.10.0",
        // "Newtonsoft.Json >= 12.0.3",
        // "NuGet.ProjectModel >= 5.6.0",
        // "NuGet.Versioning >= 5.6.0",
        // "Polly >= 7.0.3",
        // "SemanticVersioning >= 1.2.0",
        // "StyleCop.Analyzers >= 1.0.2",
        // "System.Composition.AttributedModel >= 1.4.0",
        // "System.Composition.Convention >= 1.4.0",
        // "System.Composition.Hosting >= 1.4.0",
        // "System.Composition.Runtime >= 1.4.0",
        // "System.Composition.TypedParts >= 1.4.0",
        // "System.Reactive >= 4.1.2",
        // "System.Threading.Tasks.Dataflow >= 4.9.0",
        // "coverlet.msbuild >= 2.5.1",
        // "yamldotnet >= 5.3.0"
        var expectedExplicitRefs = new[]
        {
            "DotNet.Glob",
            "Microsoft.NETCore.App",
            "MinVer",
            "Nett",
            "Newtonsoft.Json",
            "NuGet.ProjectModel",
            "NuGet.Versioning",
            "Polly",
            "SemanticVersioning",
            "StyleCop.Analyzers",
            "System.Composition.AttributedModel",
            "System.Composition.Convention",
            "System.Composition.Hosting",
            "System.Composition.Runtime",
            "System.Composition.TypedParts",
            "System.Reactive",
            "System.Threading.Tasks.Dataflow",
            "coverlet.msbuild",
            "YamlDotNet",
        };

        foreach (var componentId in graph.GetComponents())
        {
            var component = detectedComponents.First(x => x.Component.Id == componentId);
            var expectedExplicitRefValue = expectedExplicitRefs.Contains(((NuGetComponent)component.Component).Name);
            graph.IsComponentExplicitlyReferenced(componentId).Should().Be(expectedExplicitRefValue, "{0} should{1} be explicitly referenced.", componentId, expectedExplicitRefValue ? string.Empty : "n't");
        }
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_Base_3_1_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_3_1);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        // Number of unique nodes in ProjectAssetsJson
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(11);

        var nonDevComponents = detectedComponents.Where(c => !componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        nonDevComponents.Should().ContainSingle();
        nonDevComponents.Select(x => x.Component).Cast<NuGetComponent>().Single().Name.Should().StartWith("Microsoft.Extensions.DependencyModel");

        var systemTextJson = detectedComponents.FirstOrDefault(x => (x.Component as NuGetComponent).Name.Contains("System.Text.Json"));

        componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NuGetComponent>(
            systemTextJson.Component.Id,
            x => x.Name.Contains("Microsoft.Extensions.DependencyModel")).Should().BeTrue();

        foreach (var component in detectedComponents)
        {
            component.TargetFrameworks.Should().BeEquivalentTo(["netcoreapp3.1"]);
        }

        componentRecorder.ForAllComponents(grouping => grouping.AllFileLocations.Should().Contain(location => location.Contains("ExtCore.WebApplication.csproj")));
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_3_1_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_3_1);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var developmentDependencies = componentRecorder.GetDetectedComponents().Where(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        developmentDependencies.Should().HaveCount(10, "Omitted framework assemblies are missing.");
        developmentDependencies.Should().Contain(c => c.Component.Id.StartsWith("System.Reflection "), "System.Reflection should be treated as a development dependency.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_DependencyGraph_3_1_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_3_1);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var graphsByLocation = componentRecorder.GetDependencyGraphsByLocation();
        var graph = graphsByLocation.Values.First();

        var expectedDependencyIdsForExtensionsDependencyModel = new[]
        {
            "System.Text.Json 4.6.0 - NuGet",
        };

        var detectedComponents = componentRecorder.GetDetectedComponents();

        var componentDetectionCommon = detectedComponents.First(x => x.Component.Id.Contains("Microsoft.Extensions.DependencyModel"));
        var dependencies = graph.GetDependenciesForComponent(componentDetectionCommon.Component.Id);
        foreach (var expectedId in expectedDependencyIdsForExtensionsDependencyModel)
        {
            dependencies.Should().Contain(expectedId);
        }

        detectedComponents.Should().HaveSameCount(graph.GetComponents());

        // Top level dependencies look like this:
        // (we expect all non-proj and non-framework to show up as explicit refs, so those will be absent from the check)
        //
        // "Microsoft.Extensions.DependencyModel >= 3.0.0",
        // "System.Runtime.Loader >= 4.3.0"
        var expectedExplicitRefs = new[]
        {
            "Microsoft.Extensions.DependencyModel",
            "System.Runtime.Loader",
        };

        foreach (var componentId in graph.GetComponents())
        {
            var component = detectedComponents.First(x => x.Component.Id == componentId);
            var expectedExplicitRefValue = expectedExplicitRefs.Contains(((NuGetComponent)component.Component).Name);
            graph.IsComponentExplicitlyReferenced(componentId).Should().Be(expectedExplicitRefValue, "{0} should{1} be explicitly referenced.", componentId, expectedExplicitRefValue ? string.Empty : "n't");
        }
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_1x_2x_VerificationAsync()
    {
        var testResources = new[]
        {
            TestResources.project_assets_1_1_console,
            TestResources.project_assets_2_1_web,
        };

        foreach (var testResource in testResources)
        {
            var (scanResult, componentRecorder) = await this.DetectorTestUtility
                .WithFile(this.projectAssetsJsonFileName, testResource)
                .ExecuteDetectorAsync();

            var detectedComponents = componentRecorder.GetDetectedComponents();
            detectedComponents.Should().AllSatisfy(c =>
                componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).Should().BeTrue($"{c.Component.Id} should be a dev dependency"));
        }
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_1_1_web_VerificationAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, TestResources.project_assets_1_1_web)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(169, "Find expected dependencies.");

        var developmentDependencies = detectedComponents.Where(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        developmentDependencies.Should().HaveCount(122, "NETCore.App packages should be dev dependencies.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_8_0_web_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_8_0_web);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().AllSatisfy(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).Should().BeTrue(), "All should be development dependencies");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_42_15_web_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_42_15_web);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        // net42.15 is not a known framework, but it can import framework packages from the closest known framework.
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().AllSatisfy(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).Should().BeTrue(), "All should be development dependencies");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_8_0_multi_framework_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_8_0_multi_framework);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var developmentDependencies = componentRecorder.GetDetectedComponents().Where(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        developmentDependencies.Should().HaveCount(3, "Omitted framework assemblies are missing.");
        developmentDependencies.Should().Contain(c => c.Component.Id.StartsWith("Microsoft.Extensions.Primitives "), "Microsoft.Extensions.Primitives should be treated as a development dependency.");
        developmentDependencies.Should().Contain(c => c.Component.Id.StartsWith("System.IO.Packaging "), "System.IO.Packaging should be treated as a development dependency.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_6_0_8_0_multi_framework_VerificationAsync()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_6_0_8_0_multi_framework);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        var developmentDependencies = componentRecorder.GetDetectedComponents().Where(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).GetValueOrDefault());
        developmentDependencies.Should().HaveCount(2, "Omitted framework assemblies are missing.");
        developmentDependencies.Should().Contain(c => c.Component.Id.StartsWith("Microsoft.Extensions.Primitives "), "Microsoft.Extensions.Primitives should be treated as a development dependency.");
        developmentDependencies.Should().NotContain(c => c.Component.Id.StartsWith("System.IO.Packaging "), "System.IO.Packaging should not be treated as a development dependency.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_PackageDownload_VerificationAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, TestResources.project_assets_packageDownload)
            .ExecuteDetectorAsync();

        var dependencies = componentRecorder.GetDetectedComponents();
        dependencies.Should().HaveCount(3, "PackageDownload dependencies should exist.");
        dependencies.Should().AllSatisfy(c => componentRecorder.GetEffectiveDevDependencyValue(c.Component.Id).Should().BeTrue(), "All PackageDownloads should be development dependencies");
        dependencies.Select(c => c.Component).Should().AllBeOfType<NuGetComponent>();
        dependencies.Select(c => c.TargetFrameworks).Should().AllSatisfy(tfms => tfms.Should().BeEquivalentTo(["net8.0"]));
    }

    [TestMethod]
    public async Task ScanDirectory_NoPackageSpecAsync()
    {
        var osAgnostic =
            @"{
  ""version"": 3,
  ""targets"": {
    "".NETCoreApp,Version=v3.0"": {}
  },
 ""packageFolders"": {}
}";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();

        dependencyGraphs.Should().BeEmpty();
    }

    private string Convert22SampleToOSAgnostic(string project_assets)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return project_assets;
        }

        project_assets = project_assets.Replace("D:\\\\Source\\\\componentdetection-bcde\\\\src\\\\Common\\\\", "/d/source/componentdetection-bcde/src/Common/");
        project_assets = project_assets.Replace("D:\\\\Source\\\\componentdetection-bcde\\\\src\\\\Contracts\\\\", "/d/source/componentdetection-bcde/src/Contracts/");
        project_assets = project_assets.Replace("D:\\\\Source\\\\componentdetection-bcde\\\\src\\\\Detectors\\\\", "/d/source/componentdetection-bcde/src/Detectors/");
        project_assets = project_assets.Replace("D:\\\\Source\\\\componentdetection-bcde\\\\src\\\\Orchestrator\\\\", "/d/source/componentdetection-bcde/src/Orchestrator/");
        project_assets = project_assets.Replace("D:\\\\Source\\\\componentdetection-bcde\\\\src\\\\Loader\\\\", "/d/source/componentdetection-bcde/src/Loader/");

        return project_assets;
    }

    private string Convert31SampleToOSAgnostic(string project_assets)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return project_assets;
        }

        project_assets = project_assets.Replace("D:\\\\Source\\\\ExtCore\\\\src\\\\ExtCore.WebApplication\\\\", "/d/Source/ExtCore/src/ExtCore.WebApplication/");
        project_assets = project_assets.Replace("D:\\\\Source\\\\ExtCore\\\\src\\\\ExtCore.Infrastructure\\\\", "/d/Source/ExtCore/src/ExtCore.Infrastructure/");
        return project_assets;
    }
}

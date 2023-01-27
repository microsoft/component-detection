namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
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
using Newtonsoft.Json;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetProjectModelProjectCentricComponentDetectorTests
{
    private readonly string projectAssetsJsonFileName = "project.assets.json";
    private DetectorTestUtility<NuGetProjectModelProjectCentricComponentDetector> detectorTestUtility;

    [TestInitialize]
    public void TestInitialize()
    {
        var detector = new NuGetProjectModelProjectCentricComponentDetector();

        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(x => x.LogWarning(It.IsAny<string>())).Callback<string>(message => Console.WriteLine(message));
        loggerMock.Setup(x => x.LogFailedReadingFile(It.IsAny<string>(), It.IsAny<Exception>())).Callback<string, Exception>((message, exception) =>
        {
            Console.WriteLine(message);
            Console.WriteLine(exception.ToString());
        });
        detector.Logger = loggerMock.Object;

        var fileUtilityServiceMock = new Mock<IFileUtilityService>();
        fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>()))
            .Returns(true);
        detector.FileUtilityService = fileUtilityServiceMock.Object;

        this.detectorTestUtility = DetectorTestUtilityCreator.Create<NuGetProjectModelProjectCentricComponentDetector>()
            .WithDetector(detector);
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_Base_2_2_Verification()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Number of unique nodes in ProjectAssetsJson
        Console.WriteLine(string.Join(",", detectedComponents.Select(x => x.Component.Id)));
        Assert.AreEqual(3, detectedComponents.Count());
        Assert.IsNotNull(detectedComponents.Select(x => x.Component).Cast<NuGetComponent>().FirstOrDefault(x => x.Name.Contains("coverlet.msbuild")));

        Assert.IsTrue(detectedComponents.All(x =>
            componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NuGetComponent>(
                x.Component.Id,
                y => y.Id == x.Component.Id)));

        componentRecorder.ForAllComponents(grouping => Assert.IsTrue(grouping.AllFileLocations.Any(location => location.Contains("Loader.csproj"))));
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_Base_2_2_additional_Verification()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2_additional);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Number of unique nodes in ProjectAssetsJson
        Console.WriteLine(string.Join(",", detectedComponents.Select(x => x.Component.Id)));
        Assert.AreEqual(26, detectedComponents.Count());
        Assert.IsNotNull(detectedComponents.Select(x => x.Component).Cast<NuGetComponent>().FirstOrDefault(x => x.Name.Contains("Polly")));
        Assert.AreEqual(5, detectedComponents.Select(x => x.Component).Cast<NuGetComponent>().Count(x => x.Name.Contains("System.Composition")));

        var nugetVersioning = detectedComponents.FirstOrDefault(x => (x.Component as NuGetComponent).Name.Contains("NuGet.DependencyResolver.Core"));
        Assert.IsNotNull(nugetVersioning);

        Assert.IsTrue(componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NuGetComponent>(
            nugetVersioning.Component.Id,
            x => x.Name.Contains("NuGet.ProjectModel")));

        componentRecorder.ForAllComponents(grouping => Assert.IsTrue(grouping.AllFileLocations.Any(location => location.Contains("Detectors.csproj"))));
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_2_2_Verification()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

        var ommittedComponentInformationJson = scanResult.AdditionalTelemetryDetails[NuGetProjectModelProjectCentricComponentDetector.OmittedFrameworkComponentsTelemetryKey];
        var omittedComponentsWithCount = JsonConvert.DeserializeObject<Dictionary<string, int>>(ommittedComponentInformationJson);

        Assert.IsTrue(omittedComponentsWithCount.Keys.Count > 5, "Ommitted framework assemblies are missing. There should be more than ten, but this is a gut check to make sure we have data.");
        Assert.AreEqual(omittedComponentsWithCount["Microsoft.NETCore.App"], 4, "There should be four cases of the NETCore.App library being omitted in the test data.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_DependencyGraph_2_2_additional_Verification()
    {
        var osAgnostic = this.Convert22SampleToOSAgnostic(TestResources.project_assets_2_2_additional);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();
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
            Assert.IsTrue(dependencies.Contains(expectedId));
        }

        Assert.AreEqual(dependencies.Count(), expectedDependencyIdsForCompositionTypedParts.Length);

        Assert.AreEqual(graph.GetComponents().Count(), detectedComponents.Count());

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
            "coverlet.msbuild",
            "YamlDotNet",
        };

        foreach (var componentId in graph.GetComponents())
        {
            var component = detectedComponents.First(x => x.Component.Id == componentId);
            var expectedExplicitRefValue = expectedExplicitRefs.Contains(((NuGetComponent)component.Component).Name);
            Assert.AreEqual(expectedExplicitRefValue, graph.IsComponentExplicitlyReferenced(componentId));
        }
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_Base_3_1_Verification()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_3_1);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

        // Number of unique nodes in ProjectAssetsJson
        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(2, detectedComponents.Count());
        Assert.IsNotNull(detectedComponents.Select(x => x.Component).Cast<NuGetComponent>().FirstOrDefault(x => x.Name.Contains("Microsoft.Extensions.DependencyModel")));

        var systemTextJson = detectedComponents.FirstOrDefault(x => (x.Component as NuGetComponent).Name.Contains("System.Text.Json"));

        Assert.IsTrue(componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NuGetComponent>(
            systemTextJson.Component.Id,
            x => x.Name.Contains("Microsoft.Extensions.DependencyModel")));

        componentRecorder.ForAllComponents(grouping => Assert.IsTrue(grouping.AllFileLocations.Any(location => location.Contains("ExtCore.WebApplication.csproj"))));
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_ExcludedFrameworkComponent_3_1_Verification()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_3_1);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

        var ommittedComponentInformationJson = scanResult.AdditionalTelemetryDetails[NuGetProjectModelProjectCentricComponentDetector.OmittedFrameworkComponentsTelemetryKey];
        var omittedComponentsWithCount = JsonConvert.DeserializeObject<Dictionary<string, int>>(ommittedComponentInformationJson);

        // With 3.X, we don't expect there to be a lot of these, but there are still netstandard libraries present which can bring things into the graph
        Assert.AreEqual(omittedComponentsWithCount.Keys.Count, 4, "Ommitted framework assemblies are missing. There should be more than ten, but this is a gut check to make sure we have data.");
        Assert.AreEqual(omittedComponentsWithCount["System.Reflection"], 1, "There should be one case of the System.Reflection library being omitted in the test data.");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_DependencyGraph_3_1_Verification()
    {
        var osAgnostic = this.Convert31SampleToOSAgnostic(TestResources.project_assets_3_1);
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

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
            Assert.IsTrue(dependencies.Contains(expectedId));
        }

        Assert.AreEqual(graph.GetComponents().Count(), detectedComponents.Count());

        // Top level dependencies look like this:
        // (we expect all non-proj and non-framework to show up as explicit refs, so those will be absent from the check)
        //
        // "ExtCore.Infrastructure >= 5.1.0",
        // "Microsoft.Extensions.DependencyModel >= 3.0.0",
        // "System.Runtime.Loader >= 4.3.0"
        var expectedExplicitRefs = new[]
        {
            "Microsoft.Extensions.DependencyModel",
        };

        foreach (var componentId in graph.GetComponents())
        {
            var component = detectedComponents.First(x => x.Component.Id == componentId);
            var expectedExplicitRefValue = expectedExplicitRefs.Contains(((NuGetComponent)component.Component).Name);
            Assert.AreEqual(expectedExplicitRefValue, graph.IsComponentExplicitlyReferenced(componentId));
        }
    }

    [TestMethod]
    public async Task ScanDirectory_NoPackageSpec()
    {
        var osAgnostic =
            @"{
  ""version"": 3,
  ""targets"": {
    "".NETCoreApp,Version=v3.0"": {}
  },
 ""packageFolders"": {}
}";
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(this.projectAssetsJsonFileName, osAgnostic)
            .ExecuteDetector();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();

        dependencyGraphs.Count.Should().Be(0);
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

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet;

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NuGetPackagesConfigDetectorTests : BaseDetectorTest<NuGetPackagesConfigDetector>
{
    [TestMethod]
    public async Task Should_WorkAsync()
    {
        var targetFramework = "net46";
        var packagesConfig =
            @$"<?xml version=""1.0"" encoding=""utf-8""?>
                <packages>
                    <package id=""jQuery"" version=""3.1.1"" targetFramework=""{targetFramework}"" />
                    <package id=""NLog"" version=""4.3.10"" targetFramework=""{targetFramework}"" />
                </packages>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("packages.config", packagesConfig)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var jqueryDetectedComponent = new DetectedComponent(new NuGetComponent("jQuery", "3.1.1"));
        jqueryDetectedComponent.TargetFrameworks.Add(targetFramework);

        var nlogDetectedComponent = new DetectedComponent(new NuGetComponent("NLog", "4.3.10"));
        nlogDetectedComponent.TargetFrameworks.Add(targetFramework);

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(2)
            .And.ContainEquivalentOf(jqueryDetectedComponent)
            .And.ContainEquivalentOf(nlogDetectedComponent);
    }

    [TestMethod]
    public async Task Should_SkipWithInvalidVersionAsync()
    {
        var packagesConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
                <packages>
                    <package id=""jQuery"" version=""3.1.1"" targetFramework=""net46"" />
                    <package id=""NLog"" version=""
                 </packages>";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("packages.config", packagesConfig)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }
}

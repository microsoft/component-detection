namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet;

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NuGetPackagesConfigDetectorTests : BaseDetectorTest<NuGetPackagesConfigDetector>
{
    [TestMethod]
    public async Task Should_WorkAsync()
    {
        var packagesConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
                <packages>
                    <package id=""jQuery"" version=""3.1.1"" targetFramework=""net46"" />
                    <package id=""NLog"" version=""4.3.10"" targetFramework=""net46"" />
                </packages>";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("packages.config", packagesConfig)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(2)
            .And.ContainEquivalentOf(new DetectedComponent(new NuGetComponent("jQuery", "3.1.1")))
            .And.ContainEquivalentOf(new DetectedComponent(new NuGetComponent("NLog", "4.3.10")));
    }
}

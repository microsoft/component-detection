namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet
{
    using System.Threading.Tasks;
    using Microsoft.ComponentDetection.Contracts;
    using Microsoft.ComponentDetection.Contracts.TypedComponent;
    using Microsoft.ComponentDetection.Detectors.NuGet;
    using FluentAssertions;
    using Microsoft.ComponentDetection.TestsUtilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NuGetPackagesConfigDetectorTests
    {
        private DetectorTestUtility<NuGetPackagesConfigDetector> detectorTestUtility;

        [TestInitialize]
        public void TestInitialize()
        {
            var detector = new NuGetPackagesConfigDetector();
            this.detectorTestUtility = new DetectorTestUtility<NuGetPackagesConfigDetector>().WithDetector(detector);
        }

        [TestMethod]
        public async Task Should_Work()
        {
            var packagesConfig =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <packages>
                    <package id=""jQuery"" version=""3.1.1"" targetFramework=""net46"" />
                    <package id=""NLog"" version=""4.3.10"" targetFramework=""net46"" />
                </packages>";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithFile("packages.config", packagesConfig)
                .ExecuteDetector()
                .ConfigureAwait(true);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            detectedComponents.Should().NotBeEmpty()
                .And.HaveCount(2)
                .And.ContainEquivalentOf(new DetectedComponent(new NuGetComponent("jQuery", "3.1.1")))
                .And.ContainEquivalentOf(new DetectedComponent(new NuGetComponent("NLog", "4.3.10")));
        }
    }
}

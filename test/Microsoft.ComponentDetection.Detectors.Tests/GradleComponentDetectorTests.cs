using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Gradle;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class GradleComponentDetectorTests
    {
        private DetectorTestUtility<GradleComponentDetector> detectorTestUtility;

        [TestInitialize]
        public void TestInitialize()
        {
            this.detectorTestUtility = DetectorTestUtilityCreator.Create<GradleComponentDetector>();
        }

        [TestMethod]
        public async Task TestGradleDetectorWithNoFiles_ReturnsSuccessfully()
        {
            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestGradleDetectorWithValidFile_DetectsComponentsSuccessfully()
        {
            var validFileOne =
@"org.springframework:spring-beans:5.0.5.RELEASE
org.springframework:spring-core:5.0.5.RELEASE
org.springframework:spring-jcl:5.0.5.RELEASE";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("gradle.lockfile", validFileOne)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var discoveredComponents = componentRecorder.GetDetectedComponents().Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();

            Assert.AreEqual(3, discoveredComponents.Count);

            Assert.AreEqual("org.springframework", discoveredComponents[0].GroupId);
            Assert.AreEqual("spring-beans", discoveredComponents[0].ArtifactId);
            Assert.AreEqual("5.0.5.RELEASE", discoveredComponents[0].Version);

            Assert.AreEqual("org.springframework", discoveredComponents[1].GroupId);
            Assert.AreEqual("spring-core", discoveredComponents[1].ArtifactId);
            Assert.AreEqual("5.0.5.RELEASE", discoveredComponents[1].Version);

            Assert.AreEqual("org.springframework", discoveredComponents[2].GroupId);
            Assert.AreEqual("spring-jcl", discoveredComponents[2].ArtifactId);
            Assert.AreEqual("5.0.5.RELEASE", discoveredComponents[2].Version);
        }

        [TestMethod]
        public async Task TestGradleDetectorWithValidSingleLockfilePerProject_DetectsComponentsSuccessfully()
        {
            var validFileOne =
@"org.springframework:spring-beans:5.0.5.RELEASE=lintClassPath
org.springframework:spring-core:5.0.5.RELEASE=debugCompile,releaseCompile
org.springframework:spring-jcl:5.0.5.RELEASE=lintClassPath,debugCompile,releaseCompile";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("gradle.lockfile", validFileOne)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var discoveredComponents = detectedComponents.Select(c => (MavenComponent)c.Component).OrderBy(c => c.ArtifactId).ToList();

            Assert.AreEqual(3, discoveredComponents.Count);

            Assert.AreEqual("org.springframework", discoveredComponents[0].GroupId);
            Assert.AreEqual("spring-beans", discoveredComponents[0].ArtifactId);
            Assert.AreEqual("5.0.5.RELEASE", discoveredComponents[0].Version);

            Assert.AreEqual("org.springframework", discoveredComponents[1].GroupId);
            Assert.AreEqual("spring-core", discoveredComponents[1].ArtifactId);
            Assert.AreEqual("5.0.5.RELEASE", discoveredComponents[1].Version);

            Assert.AreEqual("org.springframework", discoveredComponents[2].GroupId);
            Assert.AreEqual("spring-jcl", discoveredComponents[2].ArtifactId);
            Assert.AreEqual("5.0.5.RELEASE", discoveredComponents[2].Version);
        }

        [TestMethod]
        public async Task TestGradleDetectorWithValidFiles_ReturnsSuccessfully()
        {
            var validFileOne =
@"org.springframework:spring-beans:5.0.5.RELEASE
org.springframework:spring-core:5.0.5.RELEASE
org.springframework:spring-jcl:5.0.5.RELEASE";

            var validFileTwo =
@"com.fasterxml.jackson.core:jackson-annotations:2.8.0
com.fasterxml.jackson.core:jackson-core:2.8.10
com.fasterxml.jackson.core:jackson-databind:2.8.11.3
org.msgpack:msgpack-core:0.8.16
org.springframework:spring-jcl:5.0.5.RELEASE";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("gradle.lockfile", validFileOne)
                                                    .WithFile("gradle2.lockfile", validFileTwo)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Keys.Count().Should().Be(2);

            var graph1 = dependencyGraphs.Values.Where(dependencyGraph => dependencyGraph.GetComponents().Count() == 3).Single();
            var graph2 = dependencyGraphs.Values.Where(dependencyGraph => dependencyGraph.GetComponents().Count() == 5).Single();

            var expectedComponents = new List<string>
            {
                // Graph 1
                "org.springframework spring-jcl 5.0.5.RELEASE - Maven",
                "org.springframework spring-beans 5.0.5.RELEASE - Maven",
                "org.springframework spring-core 5.0.5.RELEASE - Maven",

                // Graph 2
                "org.msgpack msgpack-core 0.8.16 - Maven",
                "org.springframework spring-jcl 5.0.5.RELEASE - Maven",
                "com.fasterxml.jackson.core jackson-core 2.8.10 - Maven",
                "com.fasterxml.jackson.core jackson-annotations 2.8.0 - Maven",
                "com.fasterxml.jackson.core jackson-databind 2.8.11.3 - Maven",
            };

            foreach (var componentId in expectedComponents)
            {
                var component = componentRecorder.GetComponent(componentId);
                component.Should().NotBeNull();
            }
        }

        [TestMethod]
        public async Task TestGradleDetector_SameComponentDifferentLocations_DifferentLocationsAreSaved()
        {
            var validFileOne =
@"org.springframework:spring-beans:5.0.5.RELEASE";

            var validFileTwo =
"org.springframework:spring-beans:5.0.5.RELEASE";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("gradle.lockfile", validFileOne)
                                                    .WithFile("gradle2.lockfile", validFileTwo)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());

            componentRecorder.ForOneComponent(componentRecorder.GetDetectedComponents().First().Component.Id, x =>
            {
                Enumerable.Count<string>(x.AllFileLocations).Should().Be(2);
            });

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Keys.Count().Should().Be(2);

            var graph1 = dependencyGraphs.Values.First();
            var graph2 = dependencyGraphs.Values.Skip(1).First();

            graph1.GetComponents().Should().BeEquivalentTo(graph2.GetComponents());
        }

        [TestMethod]
        public async Task TestGradleDetectorWithInvalidAndValidFiles_ReturnsSuccessfully()
        {
            var validFileTwo =
@"com.fasterxml.jackson.core:jackson-annotations:2.8.0
com.fasterxml.jackson.core:jackson-core:2.8.10
com.fasterxml.jackson.core:jackson-databind:2.8.11.3
org.msgpack:msgpack-core:0.8.16
org.springframework:spring-jcl:5.0.5.RELEASE";

            var invalidFileOne =
@"     #/bin/sh
lorem ipsum
four score and seven bugs ago
$#26^#25%4";

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("gradle.lockfile", invalidFileOne)
                                                    .WithFile("gradle2.lockfile", validFileTwo)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(5, componentRecorder.GetDetectedComponents().Count());

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Keys.Count().Should().Be(1);

            var graph2 = dependencyGraphs.Values.Single();

            var expectedComponents = new List<string>
            {
                // Graph 2
                "org.msgpack msgpack-core 0.8.16 - Maven",
                "org.springframework spring-jcl 5.0.5.RELEASE - Maven",
                "com.fasterxml.jackson.core jackson-core 2.8.10 - Maven",
                "com.fasterxml.jackson.core jackson-annotations 2.8.0 - Maven",
                "com.fasterxml.jackson.core jackson-databind 2.8.11.3 - Maven",
            };

            foreach (var componentId in expectedComponents)
            {
                var component = componentRecorder.GetComponent(componentId);
                component.Should().NotBeNull();
            }
        }
    }
}

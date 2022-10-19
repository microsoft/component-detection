using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class RustCrateDetectorTests
    {
        private readonly string testCargoLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""dev_dependency_dependency""
version = ""0.2.23""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""one_more_dev_dep 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)""
]

[[package]]
name = ""one_more_dev_dep""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_other_package""
version = ""1.0.0""

[[package]]
name = ""my_test_package""
version = ""1.2.3""
dependencies = [
 ""my_dependency 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""my_other_package 1.0.0"",
 ""other_dependency 0.4.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""my_dev_dependency 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[metadata]
";

        private readonly string testCargoLockV2String = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0""
]

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""dev_dependency_dependency""
version = ""0.2.23""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 2.0.0""
]

[[package]]
name = ""my_other_package""
version = ""1.0.0""

[[package]]
name = ""my_test_package""
version = ""1.2.3""
dependencies = [
 ""my_dependency 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""my_other_package 1.0.0"",
 ""other_dependency 0.4.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""my_dev_dependency 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""same_package""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""same_package""
version = ""2.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";

        private readonly string testWorkspaceLockV1NoBaseString = @"[[package]]
name = ""dev_dependency_dependency""
version = ""0.2.23""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""one_more_dev_dep 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)""
]

[[package]]
name = ""one_more_dev_dep""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)""
]

[[package]]
name = ""same_package""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[metadata]
";

        private readonly string testWorkspaceLockV2NoBaseString = @"[[package]]
name = ""dev_dependency_dependency""
version = ""0.2.23""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""one_more_dev_dep 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)""
]

[[package]]
name = ""one_more_dev_dep""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)""
]

[[package]]
name = ""same_package""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";

        private readonly string testWorkspaceLockBaseDependency = @"
[[package]]
name = ""test_package""
version = ""2.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";

        private DetectorTestUtility<RustCrateDetector> detectorTestUtility;

        [TestInitialize]
        public void TestInitialize()
        {
            this.detectorTestUtility = DetectorTestUtilityCreator.Create<RustCrateDetector>();
        }

        [TestMethod]
        public async Task TestGraphIsCorrect()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());

            var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

            // Verify explicitly referenced roots
            var rootComponents = new List<string>
            {
                "my_dependency 1.0.0 - Cargo",
                "other_dependency 0.4.0 - Cargo",
                "my_dev_dependency 1.0.0 - Cargo",
            };

            rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

            // Verify dependencies for my_dependency
            graph.GetDependenciesForComponent("my_dependency 1.0.0 - Cargo").Should().BeEmpty();

            // Verify dependencies for other_dependency
            var other_dependencyDependencies = new List<string> {
                "other_dependency_dependency 0.1.12-alpha.6 - Cargo",
            };

            graph.GetDependenciesForComponent("other_dependency 0.4.0 - Cargo").Should().BeEquivalentTo(other_dependencyDependencies);

            // Verify dependencies for my_dev_dependency
            var my_dev_dependencyDependencies = new List<string> {
                "other_dependency_dependency 0.1.12-alpha.6 - Cargo",
                "dev_dependency_dependency 0.2.23 - Cargo",
            };

            graph.GetDependenciesForComponent("my_dev_dependency 1.0.0 - Cargo").Should().BeEquivalentTo(my_dev_dependencyDependencies);
        }

        [TestMethod]
        public async Task TestSupportsCargoV1AndV2DefinitionPairs()
        {
            var componentRecorder = new ComponentRecorder();
            var request = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), null, null, new Dictionary<string, string>(), null, componentRecorder);

            var (result, _) = await this.detectorTestUtility
                                                    /* v1 files */
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    /* v2 files */
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String, fileLocation: Path.Join(Path.GetTempPath(), "v2", "Cargo.lock"))
                                                    /* so we can reuse the component recorder */
                                                    .WithScanRequest(request)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            componentGraphs.Count.Should().Be(2); // 1 for each detector
        }

        [TestMethod]
        public async Task TestSupportsMultipleCargoV1DefinitionPairs()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.lock", this.testCargoLockString, fileLocation: Path.Join(Path.GetTempPath(), "sub-path", "Cargo.lock"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            componentGraphs.Count.Should().Be(2); // 1 graph for each Cargo.lock

            var graph1 = componentGraphs.Values.First();
            var graph2 = componentGraphs.Values.Skip(1).First();

            graph1.GetComponents().Should().BeEquivalentTo(graph2.GetComponents()); // The graphs should have detected the same components

            // Two Cargo.lock files
            componentRecorder.ForAllComponents(x => Enumerable.Count<string>(x.AllFileLocations).Should().Be(2));
        }

        [TestMethod]
        public async Task TestSupportsMultipleCargoV2DefinitionPairs()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String)
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String, fileLocation: Path.Join(Path.GetTempPath(), "sub-path", "Cargo.lock"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            componentGraphs.Count.Should().Be(2); // 1 graph for each Cargo.lock

            var graph1 = componentGraphs.Values.First();
            var graph2 = componentGraphs.Values.Skip(1).First();

            graph1.GetComponents().Should().BeEquivalentTo(graph2.GetComponents()); // The graphs should have detected the same components

            // Two Cargo.lock files
            componentRecorder.ForAllComponents(x => x.AllFileLocations.Count().Should().Be(2));
        }

        [TestMethod]
        public async Task TestRustDetector()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());

            IDictionary<string, string> packageVersions = new Dictionary<string, string>()
            {
                { "my_dependency", "1.0.0" },
                { "other_dependency", "0.4.0" },
                { "other_dependency_dependency", "0.1.12-alpha.6" },
                { "my_dev_dependency", "1.0.0" },
                { "dev_dependency_dependency", "0.2.23" },
                { "one_more_dev_dep", "1.0.0" },
            };

            IDictionary<string, ISet<string>> packageDependencyRoots = new Dictionary<string, ISet<string>>()
            {
                { "my_dependency", new HashSet<string>() { "my_dependency" } },
                { "other_dependency", new HashSet<string>() { "other_dependency" } },
                { "other_dependency_dependency", new HashSet<string>() { "other_dependency", "my_dev_dependency" } },
                { "my_dev_dependency", new HashSet<string>() { "my_dev_dependency" } },
                { "dev_dependency_dependency", new HashSet<string>() { "my_dev_dependency" } },
                { "one_more_dev_dep", new HashSet<string>() { "my_dev_dependency" } },
            };

            ISet<string> componentNames = new HashSet<string>();
            foreach (var discoveredComponent in componentRecorder.GetDetectedComponents())
            {
                // Verify each package has the right information
                var packageName = (discoveredComponent.Component as CargoComponent).Name;

                // Verify version
                Assert.AreEqual(packageVersions[packageName], (discoveredComponent.Component as CargoComponent).Version);

                var dependencyRoots = new HashSet<string>();

                componentRecorder.AssertAllExplicitlyReferencedComponents(
                    discoveredComponent.Component.Id,
                    packageDependencyRoots[packageName].Select(expectedRoot =>
                        new Func<CargoComponent, bool>(parentComponent => parentComponent.Name == expectedRoot)).ToArray());

                componentNames.Add(packageName);
            }

            // Verify all packages were detected
            foreach (var expectedPackage in packageVersions.Keys)
            {
                Assert.IsTrue(componentNames.Contains(expectedPackage));
            }
        }

        [TestMethod]
        public async Task TestRustV2Detector()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

            var packageVersions = new List<string>()
            {
                "my_dependency 1.0.0",
                "other_dependency 0.4.0",
                "other_dependency_dependency 0.1.12-alpha.6",
                "my_dev_dependency 1.0.0",
                "dev_dependency_dependency 0.2.23",
                "same_package 1.0.0",
                "same_package 2.0.0",
            };

            IDictionary<string, ISet<string>> packageDependencyRoots = new Dictionary<string, ISet<string>>()
            {
                { "my_dependency 1.0.0", new HashSet<string>() { "my_dependency 1.0.0" } },
                { "other_dependency 0.4.0", new HashSet<string>() { "other_dependency 0.4.0" } },
                { "other_dependency_dependency 0.1.12-alpha.6", new HashSet<string>() { "other_dependency 0.4.0", "my_dev_dependency 1.0.0" } },
                { "my_dev_dependency 1.0.0", new HashSet<string>() { "my_dev_dependency 1.0.0" } },
                { "dev_dependency_dependency 0.2.23", new HashSet<string>() { "my_dev_dependency 1.0.0" } },
                { "same_package 2.0.0", new HashSet<string>() { "my_dev_dependency 1.0.0" } },
                { "same_package 1.0.0",  new HashSet<string>() { "my_dependency 1.0.0" } },
            };

            ISet<string> componentNames = new HashSet<string>();
            foreach (var discoveredComponent in componentRecorder.GetDetectedComponents())
            {
                var component = discoveredComponent.Component as CargoComponent;
                var componentKey = $"{component.Name} {component.Version}";

                // Verify version
                Assert.IsTrue(packageVersions.Contains(componentKey));

                componentRecorder.AssertAllExplicitlyReferencedComponents(
                    discoveredComponent.Component.Id,
                    packageDependencyRoots[componentKey].Select(expectedRoot =>
                        new Func<CargoComponent, bool>(parentComponent => $"{parentComponent.Name} {parentComponent.Version}" == expectedRoot)).ToArray());

                componentNames.Add(componentKey);
            }

            // Verify all packages were detected
            foreach (var expectedPackage in packageVersions)
            {
                Assert.IsTrue(componentNames.Contains(expectedPackage));
            }
        }

        [TestMethod]
        public async Task TestRustV2Detector_DuplicatePackage()
        {
            var testCargoLock = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 1.0.0""
]

[[package]]
name = ""other_dependency""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""my_dev_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""dev_dependency_dependency 0.2.23 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""dev_dependency_dependency""
version = ""0.2.23""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
  ""same_package 2.0.0""
]

[[package]]
name = ""my_other_package""
version = ""1.0.0""

[[package]]
name = ""my_test_package""
version = ""1.2.3""
dependencies = [
 ""my_dependency 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""my_other_package 1.0.0"",
 ""other_dependency 0.4.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""my_dev_dependency 1.0.0 (registry+https://github.com/rust-lang/crates.io-index)"",
]

[[package]]
name = ""same_package""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""same_package""
version = ""2.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";

            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", testCargoLock)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

            var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

            // Verify explicitly referenced roots
            var rootComponents = new List<string>
            {
                "my_dependency 1.0.0 - Cargo",
                "my_dev_dependency 1.0.0 - Cargo",
                "other_dependency 0.4.0 - Cargo",
            };

            rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

            // Verify dependencies for my_dependency
            var my_dependencyDependencies = new List<string> {
                "same_package 1.0.0 - Cargo",
            };

            graph.GetDependenciesForComponent("my_dependency 1.0.0 - Cargo").Should().BeEquivalentTo(my_dependencyDependencies);

            // Verify dependencies for other_dependency
            var other_dependencyDependencies = new List<string> { "other_dependency_dependency 0.1.12-alpha.6 - Cargo" };

            graph.GetDependenciesForComponent("other_dependency 0.4.0 - Cargo").Should().BeEquivalentTo(other_dependencyDependencies);

            // Verify dependencies for my_dev_dependency
            var my_dev_dependencyDependencies = new List<string> {
                "other_dependency_dependency 0.1.12-alpha.6 - Cargo",
                "dev_dependency_dependency 0.2.23 - Cargo",
            };

            graph.GetDependenciesForComponent("my_dev_dependency 1.0.0 - Cargo").Should().BeEquivalentTo(my_dev_dependencyDependencies);
        }

        [TestMethod]
        public async Task TestRustDetector_SupportEmptySource()
        {
            var testLockString = @"
[[package]]
name = ""my_test_package""
version = ""1.2.3""
dependencies = [
  ""my_dependency""
]

[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
dependencies = [
 ""other_dependency_dependency 0.1.12-alpha.6 ()"",
]

[[package]]
name = ""other_dependency_dependency""
version = ""0.1.12-alpha.6""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", testLockString)
                                                    .ExecuteDetector();

            result.ResultCode.Should().Be(ProcessingResultCode.Success);

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Count.Should().Be(1);

            var dependencyGraph = dependencyGraphs.Single().Value;
            var foundComponents = dependencyGraph.GetComponents();
            foundComponents.Count().Should().Be(2);

            componentRecorder.ForOneComponent("other_dependency_dependency 0.1.12-alpha.6 - Cargo", (grouping) =>
            {
                grouping.ParentComponentIdsThatAreExplicitReferences.Should().BeEquivalentTo("my_dependency 1.0.0 - Cargo");
            });
        }

        [TestMethod]
        public async Task TestRustDetector_V1WorkspacesWithTopLevelDependencies()
        {
            await this.TestRustDetector_WorkspacesWithTopLevelDependencies(this.testWorkspaceLockV1NoBaseString);
        }

        [TestMethod]
        public async Task TestRustDetector_V2WorkspacesWithTopLevelDependencies()
        {
            await this.TestRustDetector_WorkspacesWithTopLevelDependencies(this.testWorkspaceLockV2NoBaseString);
        }

        private async Task TestRustDetector_WorkspacesWithTopLevelDependencies(string lockFile)
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", string.Concat(this.testWorkspaceLockBaseDependency, lockFile))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

            var packageVersions = new List<string>()
            {
                "dev_dependency_dependency 0.2.23",
                "one_more_dev_dep 1.0.0",
                "other_dependency 0.4.0",
                "other_dependency_dependency 0.1.12-alpha.6",
                "my_dependency 1.0.0",
                "same_package 1.0.0",
                "test_package 2.0.0",
            };

            IDictionary<string, ISet<string>> packageDependencyRoots = new Dictionary<string, ISet<string>>()
            {
                { "dev_dependency_dependency 0.2.23", new HashSet<string>() { "dev_dependency_dependency 0.2.23" } },
                { "one_more_dev_dep 1.0.0", new HashSet<string>() { "dev_dependency_dependency 0.2.23" } },
                { "other_dependency 0.4.0", new HashSet<string>() { "other_dependency 0.4.0" } },
                { "other_dependency_dependency 0.1.12-alpha.6", new HashSet<string>() { "other_dependency 0.4.0" } },
                { "my_dependency 1.0.0", new HashSet<string>() { "my_dependency 1.0.0" } },
                { "same_package 1.0.0",  new HashSet<string>() { "my_dependency 1.0.0" } },
                { "test_package 2.0.0", new HashSet<string>() { "test_package 2.0.0" } },
            };

            ISet<string> componentNames = new HashSet<string>();
            foreach (var discoveredComponent in componentRecorder.GetDetectedComponents())
            {
                var component = discoveredComponent.Component as CargoComponent;
                var componentKey = $"{component.Name} {component.Version}";

                // Verify version
                Assert.IsTrue(packageVersions.Contains(componentKey));

                componentRecorder.AssertAllExplicitlyReferencedComponents(
                    discoveredComponent.Component.Id,
                    packageDependencyRoots[componentKey].Select(expectedRoot =>
                        new Func<CargoComponent, bool>(parentComponent => $"{parentComponent.Name} {parentComponent.Version}" == expectedRoot)).ToArray());

                componentNames.Add(componentKey);
            }

            // Verify all packages were detected
            foreach (var expectedPackage in packageVersions)
            {
                Assert.IsTrue(componentNames.Contains(expectedPackage));
            }
        }

        [TestMethod]
        public async Task TestRustDetector_V1WorkspacesNoTopLevelDependencies()
        {
            await this.TestRustDetector_WorkspacesNoTopLevelDependencies(this.testWorkspaceLockV1NoBaseString);
        }

        [TestMethod]
        public async Task TestRustDetector_V2WorkspacesNoTopLevelDependencies()
        {
            await this.TestRustDetector_WorkspacesNoTopLevelDependencies(this.testWorkspaceLockV2NoBaseString);
        }

        private async Task TestRustDetector_WorkspacesNoTopLevelDependencies(string lockFile)
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", lockFile)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestRustDetector_V1WorkspacesWithSubDirectories()
        {
            await this.TestRustDetector_WorkspacesWithSubDirectories(this.testWorkspaceLockV1NoBaseString);
        }

        [TestMethod]
        public async Task TestRustDetector_V2WorkspacesWithSubDirectories()
        {
            await this.TestRustDetector_WorkspacesWithSubDirectories(this.testWorkspaceLockV2NoBaseString);
        }

        private async Task TestRustDetector_WorkspacesWithSubDirectories(string lockFile)
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", lockFile)
                                                    .ExecuteDetector();

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());

            Assert.AreEqual(1, componentGraphs.Count); // Only 1 Cargo.lock is specified

            // A root Cargo.lock
            componentRecorder.ForAllComponents(x => x.AllFileLocations.Count().Should().Be(1));
        }

        [TestMethod]
        public async Task TestRustDetector_UnequalButSemverCompatibleRoot()
        {
            var testLockString = @"
[[package]]
name = ""c-ares""
version = ""7.5.2""
source = ""registry+https://github.com/rust-lang/crates.io-index""
checksum = ""a8554820e0b20a1b58b4626a3477fa4bccb1f8ee75c61ef547d50523a517126f""
dependencies = [
 ""c-ares-sys"",
]

[[package]]
name = ""c-ares-sys""
version = ""5.3.3""
source = ""registry+https://github.com/rust-lang/crates.io-index""
checksum = ""067403b940b1320de22c347323f2cfd20b7c64b5709ab47928f5eb000e585fe0""

[[package]]
name = ""test""
version = ""0.1.0""
dependencies = [
 ""c-ares"",
]
";
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", testLockString)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());

            var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

            // Verify explicitly referenced roots
            var rootComponents = new List<string>
            {
                "c-ares 7.5.2 - Cargo",
            };

            rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

            // Verify dependencies for other_dependency
            var cAresDependencies = new List<string> { "c-ares-sys 5.3.3 - Cargo" };
            graph.GetDependenciesForComponent("c-ares 7.5.2 - Cargo").Should().BeEquivalentTo(cAresDependencies);
        }

        [TestMethod]
        public async Task TestRustDetector_GitDependency()
        {
            var testLockString = @"
[[package]]
name = ""my_git_dep_test""
version = ""0.1.0""
dependencies = [
 ""my_git_dep"",
]

[[package]]
name = ""my_git_dep""
version = ""0.1.0""
source = ""git+https://github.com/microsoft/component-detection/?branch=main#abcdabcdabcdabcdabcdbacdbacdbacdabcdabcd""
";
            var (result, componentRecorder) = await this.detectorTestUtility
                                        .WithFile("Cargo.lock", testLockString)
                                        .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Count.Should().Be(1);

            var dependencyGraph = dependencyGraphs.Single().Value;
            dependencyGraph.Contains("my_git_dep 0.1.0 - Cargo").Should().BeTrue();
        }

        [TestMethod]
        public async Task TestRustDetector_MultipleRegistries()
        {
            var testLockString = @"
[[package]]
name = ""registrytest""
version = ""0.1.0""
dependencies = [
 ""common_name 0.2.0 (registry+https://github.com/rust-lang/crates.io-index)"",
 ""common_name 0.2.0 (registry+sparse+https://other.registry/index/)"",
]

[[package]]
name = ""common_name""
version = ""0.2.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""common_name""
version = ""0.2.0""
source = ""registry+sparse+https://other.registry/index/""
";
            var (result, componentRecorder) = await this.detectorTestUtility
                                        .WithFile("Cargo.lock", testLockString)
                                        .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            // If registries have identity, this should be 2
            Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Count.Should().Be(1);

            var dependencyGraph = dependencyGraphs.Single().Value;

            // If registries have identity, we should have two entries here
            var componentIds = new List<string>
            {
                "common_name 0.2.0 - Cargo",
            };

            componentIds.ForEach(componentId => dependencyGraph.Contains(componentId).Should().BeTrue());
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.Tests
{
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

    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class RustCrateDetectorTests
    {
        private DetectorTestUtility<RustCrateDetector> detectorTestUtility;
        private DetectorTestUtility<RustCrateV2Detector> detectorV2TestUtility;

        /// <summary>
        /// (my_dependency, 1.0, root)
        /// (my_other_dependency, 0.1.0, root)
        /// (other_dependency, 0.4, root) -> (other_dependency_dependency, 0.1.12-alpha.6)
        /// (my_dev_dependency, 1.0, root, dev) -> (other_dependency_dependency, 0.1.12-alpha.6)
        ///                                     -> (dev_dependency_dependency, 0.2.23, dev) -> (one_more_dev_dep, 1.0.0, dev).
        /// </summary>
        private readonly string testCargoTomlString = @"
[package]
name = ""my_test_package""
version = ""1.2.3""
authors = [""example@example.com>""]

[dependencies]
my_dependency = ""1.0""
my_other_package = { path = ""../my_other_package_path"", version = ""0.1.0"" }
other_dependency = { version = ""0.4"" }

[dev-dependencies]
my_dev_dependency = ""1.0""
";

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

        private readonly string testWorkspacesBaseTomlString = @"[workspace]
members = [
    ""test-work"",
    ""test-work2"",
]
";

        private readonly string testWorkspacesSubdirectoryTomlString = @"[workspace]
members = [
    ""sub/test-work"",
    ""sub2/test/test-work2"",
]
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

        private readonly string testWorkspaceTomlBaseDependency = @"
[dependencies]
test_package = ""2.0.0""
";

        private readonly string testWorkspace1TomlString = @"
[dependencies]
my_dependency = ""1.0.0""

[dev-dependencies]
dev_dependency_dependency = ""0.2.23""
";

        private readonly string testWorkspace2TomlString = @"
[dependencies]
other_dependency = ""0.4.0""
";

        private readonly string testTargetSpecificDependenciesTomlString = @"
[package]
name = ""my_test_package""
version = ""1.2.3""
authors = [""example@example.com>""]

[dependencies]
my_dependency = ""1.0""

[target.'cfg(windows)'.dependencies]
winhttp = ""0.4.0""

[target.'cfg(unix)'.dev-dependencies]
openssl = ""1.0.1""
";

        private readonly string testTargetSpecificDependenciesLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""winhttp""
version = ""0.4.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""

[[package]]
name = ""openssl""
version = ""1.0.1""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";

        [TestInitialize]
        public void TestInitialize()
        {
            this.detectorTestUtility = DetectorTestUtilityCreator.Create<RustCrateDetector>();
            this.detectorV2TestUtility = DetectorTestUtilityCreator.Create<RustCrateV2Detector>();
        }

        [TestMethod]
        public async Task TestGraphIsCorrect()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());

            var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

            // Verify explicitly referenced roots
            var rootComponents = new List<string>
            {
                "my_dependency 1.0.0 - Cargo",

                // Note: my_other_dependency isn't here because we don't capture local deps
                "other_dependency 0.4.0 - Cargo",
            };

            rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

            // Verify explicitly referenced dev roots
            var rootDevComponents = new List<string> { "my_dev_dependency 1.0.0 - Cargo" };

            rootDevComponents.ForEach(rootDevComponentId => graph.IsComponentExplicitlyReferenced(rootDevComponentId).Should().BeTrue());

            // Verify dependencies for my_dependency
            graph.GetDependenciesForComponent("my_dependency 1.0.0 - Cargo").Should().BeEmpty();

            // Verify dependencies for other_dependency
            var other_dependencyDependencies = new List<string> { "other_dependency_dependency 0.1.12-alpha.6 - Cargo" };

            graph.GetDependenciesForComponent("other_dependency 0.4.0 - Cargo").Should().BeEquivalentTo(other_dependencyDependencies);

            // Verify dependencies for my_dev_dependency
            var my_dev_dependencyDependencies = new List<string> { "other_dependency_dependency 0.1.12-alpha.6 - Cargo", "dev_dependency_dependency 0.2.23 - Cargo" };

            graph.GetDependenciesForComponent("my_dev_dependency 1.0.0 - Cargo").Should().BeEquivalentTo(my_dev_dependencyDependencies);
        }

        [TestMethod]
        public async Task TestRequirePairForComponents()
        {
            var cargoDefinitionPairsMatrix = new List<(string, string)>
            {
                (null, this.testCargoTomlString),
                (this.testCargoLockString, null),
                (null, null),
            };

            foreach (var cargoDefinitionPairs in cargoDefinitionPairsMatrix)
            {
                if (cargoDefinitionPairs.Item1 != null)
                {
                    this.detectorTestUtility.WithFile("Cargo.lock", cargoDefinitionPairs.Item1);
                }

                if (cargoDefinitionPairs.Item2 != null)
                {
                    this.detectorTestUtility.WithFile("Cargo.toml", cargoDefinitionPairs.Item2, new List<string> { "Cargo.toml" });
                }

                var (result, componentRecorder) = await this.detectorTestUtility.ExecuteDetector();

                Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

                componentRecorder.GetDetectedComponents().Count().Should().Be(0);
            }
        }

        [TestMethod]
        public async Task TestSupportsCargoV1AndV2DefinitionPairs()
        {
            var componentRecorder = new ComponentRecorder();
            var request = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), null, null, new Dictionary<string, string>(), null, componentRecorder);

            var (result1, _) = await this.detectorTestUtility
                                                    /* v1 files */
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    /* v2 files */
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String, fileLocation: Path.Join(Path.GetTempPath(), "v2", "Cargo.lock"))
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Join(Path.GetTempPath(), "v2", "Cargo.toml"))
                                                    /* so we can reuse the component recorder */
                                                    .WithScanRequest(request)
                                                    .ExecuteDetector();

            var (result2, _) = await this.detectorV2TestUtility
                                                    /* v1 files */
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    /* v2 files */
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String, fileLocation: Path.Join(Path.GetTempPath(), "v2", "Cargo.lock"))
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Join(Path.GetTempPath(), "v2", "Cargo.toml"))
                                                    /* so we can reuse the component recorder */
                                                    .WithScanRequest(request)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result1.ResultCode);
            Assert.AreEqual(ProcessingResultCode.Success, result2.ResultCode);

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            componentGraphs.Count.Should().Be(2); // 1 for each detector
        }

        [TestMethod]
        public async Task TestSupportsMultipleCargoV1DefinitionPairs()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.lock", this.testCargoLockString, fileLocation: Path.Join(Path.GetTempPath(), "sub-path", "Cargo.lock"))
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Join(Path.GetTempPath(), "sub-path", "Cargo.toml"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            componentGraphs.Count.Should().Be(2); // 1 graph for each Cargo.lock

            var graph1 = componentGraphs.Values.First();
            var graph2 = componentGraphs.Values.Skip(1).First();

            graph1.GetComponents().Should().BeEquivalentTo(graph2.GetComponents()); // The graphs should have detected the same components

            // 4 file locations are expected. 2 for each Cargo.lock and Cargo.toml pair
            componentRecorder.ForAllComponents(x => Enumerable.Count<string>(x.AllFileLocations).Should().Be(4));
        }

        [TestMethod]
        public async Task TestSupportsMultipleCargoV2DefinitionPairs()
        {
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String, fileLocation: Path.Join(Path.GetTempPath(), "sub-path", "Cargo.lock"))
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Join(Path.GetTempPath(), "sub-path", "Cargo.toml"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            componentGraphs.Count.Should().Be(2); // 1 graph for each Cargo.lock

            var graph1 = componentGraphs.Values.First();
            var graph2 = componentGraphs.Values.Skip(1).First();

            graph1.GetComponents().Should().BeEquivalentTo(graph2.GetComponents()); // The graphs should have detected the same components

            // 4 file locations are expected. 2 for each Cargo.lock and Cargo.toml pair
            componentRecorder.ForAllComponents(x => x.AllFileLocations.Count().Should().Be(4));
        }

        [TestMethod]
        public async Task TestRustDetector()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
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

            IDictionary<string, bool> packageIsDevDependency = new Dictionary<string, bool>()
            {
                { "my_dependency 1.0.0 - Cargo", false },
                { "other_dependency 0.4.0 - Cargo", false },
                { "other_dependency_dependency 0.1.12-alpha.6 - Cargo", false },
                { "my_dev_dependency 1.0.0 - Cargo", true },
                { "dev_dependency_dependency 0.2.23 - Cargo", true },
                { "one_more_dev_dep 1.0.0 - Cargo", true },
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

                // Verify dev dependency flag
                componentRecorder.GetEffectiveDevDependencyValue(discoveredComponent.Component.Id).Should().Be(packageIsDevDependency[discoveredComponent.Component.Id]);

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
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
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

            IDictionary<string, bool> packageIsDevDependency = new Dictionary<string, bool>()
            {
                { "my_dependency 1.0.0 - Cargo", false },
                { "other_dependency 0.4.0 - Cargo", false },
                { "other_dependency_dependency 0.1.12-alpha.6 - Cargo", false },
                { "my_dev_dependency 1.0.0 - Cargo", true },
                { "dev_dependency_dependency 0.2.23 - Cargo", true },
                { "same_package 1.0.0 - Cargo", false },
                { "same_package 2.0.0 - Cargo", true },
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

                // Verify dev dependency flag
                componentRecorder.GetEffectiveDevDependencyValue(discoveredComponent.Component.Id).Should().Be(packageIsDevDependency[discoveredComponent.Component.Id]);

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
        public async Task TestRustV2Detector_DoesNotRunV1Format()
        {
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockString)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestRustV1Detector_DoesNotRunV2Format()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testCargoLockV2String)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
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

            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", testCargoLock)
                                                    .WithFile("Cargo.toml", this.testCargoTomlString, new List<string> { "Cargo.toml" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

            var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

            // Verify explicitly referenced roots
            var rootComponents = new List<string>
            {
                "my_dependency 1.0.0 - Cargo",

                // Note: my_other_dependency isn't here because we don't capture local deps
                "other_dependency 0.4.0 - Cargo",
            };

            rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

            // Verify explicitly referenced dev roots
            var rootDevComponents = new List<string> { "my_dev_dependency 1.0.0 - Cargo" };

            rootDevComponents.ForEach(rootDevComponentId => graph.IsComponentExplicitlyReferenced(rootDevComponentId).Should().BeTrue());

            // Verify dependencies for my_dependency
            var my_dependencyDependencies = new List<string> { "same_package 1.0.0 - Cargo" };

            graph.GetDependenciesForComponent("my_dependency 1.0.0 - Cargo").Should().BeEquivalentTo(my_dependencyDependencies);

            // Verify dependencies for other_dependency
            var other_dependencyDependencies = new List<string> { "other_dependency_dependency 0.1.12-alpha.6 - Cargo" };

            graph.GetDependenciesForComponent("other_dependency 0.4.0 - Cargo").Should().BeEquivalentTo(other_dependencyDependencies);

            // Verify dependencies for my_dev_dependency
            var my_dev_dependencyDependencies = new List<string> { "other_dependency_dependency 0.1.12-alpha.6 - Cargo", "dev_dependency_dependency 0.2.23 - Cargo" };

            graph.GetDependenciesForComponent("my_dev_dependency 1.0.0 - Cargo").Should().BeEquivalentTo(my_dev_dependencyDependencies);
        }

        [TestMethod]
        public async Task TestRustDetector_SupportEmptySource()
        {
            var testTomlString = @"
[package]
name = ""my_test_package""
version = ""1.2.3""
authors = [""example@example.com>""]

[dependencies]
my_dependency = ""1.0""
";
            var testLockString = @"
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
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", testLockString)
                                                    .WithFile("Cargo.toml", testTomlString, new List<string> { "Cargo.toml" })
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
        public async Task TestRustV1Detector_WorkspacesWithTopLevelDependencies()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", string.Concat(this.testWorkspaceLockBaseDependency, this.testWorkspaceLockV1NoBaseString))
                                                    .WithFile("Cargo.toml", string.Concat(this.testWorkspaceTomlBaseDependency, this.testWorkspacesBaseTomlString), new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.toml", this.testWorkspace1TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work", "Cargo.toml"))
                                                    .WithFile("Cargo.toml", this.testWorkspace2TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work2", "Cargo.toml"))
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

            IDictionary<string, bool> packageIsDevDependency = new Dictionary<string, bool>()
            {
                { "dev_dependency_dependency 0.2.23 - Cargo", true },
                { "one_more_dev_dep 1.0.0 - Cargo", true },
                { "other_dependency 0.4.0 - Cargo", false },
                { "other_dependency_dependency 0.1.12-alpha.6 - Cargo", false },
                { "my_dependency 1.0.0 - Cargo", false },
                { "same_package 1.0.0 - Cargo", false },
                { "test_package 2.0.0 - Cargo", false },
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

                // Verify dev dependency flag
                componentRecorder.GetEffectiveDevDependencyValue(discoveredComponent.Component.Id).Should().Be(packageIsDevDependency[discoveredComponent.Component.Id]);

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
        public async Task TestRustV2Detector_WorkspacesWithTopLevelDependencies()
        {
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", string.Concat(this.testWorkspaceLockBaseDependency, this.testWorkspaceLockV2NoBaseString))
                                                    .WithFile("Cargo.toml", string.Concat(this.testWorkspaceTomlBaseDependency, this.testWorkspacesBaseTomlString), new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.toml", this.testWorkspace1TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work", "Cargo.toml"))
                                                    .WithFile("Cargo.toml", this.testWorkspace2TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work2", "Cargo.toml"))
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

            IDictionary<string, bool> packageIsDevDependency = new Dictionary<string, bool>()
            {
                { "dev_dependency_dependency 0.2.23 - Cargo", true },
                { "one_more_dev_dep 1.0.0 - Cargo", true },
                { "other_dependency 0.4.0 - Cargo", false },
                { "other_dependency_dependency 0.1.12-alpha.6 - Cargo", false },
                { "my_dependency 1.0.0 - Cargo", false },
                { "same_package 1.0.0 - Cargo", false },
                { "test_package 2.0.0 - Cargo", false },
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

                // Verify dev dependency flag
                componentRecorder.GetEffectiveDevDependencyValue(discoveredComponent.Component.Id).Should().Be(packageIsDevDependency[discoveredComponent.Component.Id]);

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
        public async Task TestRustV1Detector_WorkspacesNoTopLevelDependencies()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testWorkspaceLockV1NoBaseString)
                                                    .WithFile("Cargo.toml", this.testWorkspacesBaseTomlString, new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.toml", this.testWorkspace1TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work", "Cargo.toml"))
                                                    .WithFile("Cargo.toml", this.testWorkspace2TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work2", "Cargo.toml"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestRustV2Detector_WorkspacesNoTopLevelDependencies()
        {
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", this.testWorkspaceLockV2NoBaseString)
                                                    .WithFile("Cargo.toml", this.testWorkspacesBaseTomlString, new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.toml", this.testWorkspace1TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work", "Cargo.toml"))
                                                    .WithFile("Cargo.toml", this.testWorkspace2TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "test-work2", "Cargo.toml"))
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestRustV1Detector_WorkspacesWithSubDirectories()
        {
            var (result, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("Cargo.lock", this.testWorkspaceLockV1NoBaseString)
                                                    .WithFile("Cargo.toml", this.testWorkspacesSubdirectoryTomlString, new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.toml", this.testWorkspace1TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "sub//test-work", "Cargo.toml"))
                                                    .WithFile("Cargo.toml", this.testWorkspace2TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "sub2//test//test-work2", "Cargo.toml"))
                                                    .ExecuteDetector();

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());

            Assert.AreEqual(1, componentGraphs.Count); // Only 1 cargo.lock is specified with multiple sub-directories of .toml

            // A root Cargo.lock, Cargo.toml, and the 2 workspace Cargo.tomls should be registered
            componentRecorder.ForAllComponents(x => x.AllFileLocations.Count().Should().Be(4));
        }

        [TestMethod]
        public async Task TestRustV2Detector_WorkspacesWithSubDirectories()
        {
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", this.testWorkspaceLockV2NoBaseString)
                                                    .WithFile("Cargo.toml", this.testWorkspacesSubdirectoryTomlString, new List<string> { "Cargo.toml" })
                                                    .WithFile("Cargo.toml", this.testWorkspace1TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "sub//test-work", "Cargo.toml"))
                                                    .WithFile("Cargo.toml", this.testWorkspace2TomlString, new List<string> { "Cargo.toml" }, fileLocation: Path.Combine(Path.GetTempPath(), "sub2//test//test-work2", "Cargo.toml"))
                                                    .ExecuteDetector();

            var componentGraphs = componentRecorder.GetDependencyGraphsByLocation();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(6, componentRecorder.GetDetectedComponents().Count());

            Assert.AreEqual(1, componentGraphs.Count); // Only 1 cargo.lock is specified with multiple sub-directories of .toml

            // A root Cargo.lock, Cargo.toml, and the 2 workspace Cargo.tomls should be registered
            componentRecorder.ForAllComponents(x => x.AllFileLocations.Count().Should().Be(4));
        }

        [TestMethod]
        public async Task TestRustDetector_UnequalButSemverCompatibleRoot()
        {
            var testTomlString = @"
[package]
name = ""test""
version = ""0.1.0""
edition = ""2021""

[dependencies]
c-ares = ""7.1.0""
";
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
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", testLockString)
                                                    .WithFile("Cargo.toml", testTomlString, new List<string> { "Cargo.toml" })
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
        public async Task TestRustDetector_RenamedDependency()
        {
            var testTomlString = @"
[package]
name = ""my_test_package""
version = ""1.2.3""
authors = [""example@example.com>""]

[dependencies]
foo_dependency = { package = ""my_dependency"", version = ""1.0.0""}
";
            var testLockString = @"
[[package]]
name = ""my_dependency""
version = ""1.0.0""
source = ""registry+https://github.com/rust-lang/crates.io-index""
";
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", testLockString)
                                                    .WithFile("Cargo.toml", testTomlString, new List<string> { "Cargo.toml" })
                                                    .ExecuteDetector();

            result.ResultCode.Should().Be(ProcessingResultCode.Success);

            var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation();
            dependencyGraphs.Count.Should().Be(1);

            var dependencyGraph = dependencyGraphs.Single().Value;
            var foundComponents = dependencyGraph.GetComponents();
            foundComponents.Count().Should().Be(1);

            var rootComponents = new List<string>
            {
                "my_dependency 1.0.0 - Cargo",
            };
            rootComponents.ForEach(rootComponentId => dependencyGraph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());
        }

        [TestMethod]
        public async Task TestRustDetector_TargetSpecificDependencies()
        {
            var (result, componentRecorder) = await this.detectorV2TestUtility
                                                    .WithFile("Cargo.lock", this.testTargetSpecificDependenciesLockString)
                                                    .WithFile("Cargo.toml", this.testTargetSpecificDependenciesTomlString, new List<string> { "Cargo.toml" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
            Assert.AreEqual(3, componentRecorder.GetDetectedComponents().Count());

            componentRecorder.GetComponent("my_dependency 1.0.0 - Cargo").Should().NotBeNull();
            componentRecorder.GetComponent("winhttp 0.4.0 - Cargo").Should().NotBeNull();

            var openssl = componentRecorder.GetComponent("openssl 1.0.1 - Cargo");
            openssl.Should().NotBeNull();
            componentRecorder.GetEffectiveDevDependencyValue(openssl.Id).Value.Should().BeTrue();
        }
    }
}

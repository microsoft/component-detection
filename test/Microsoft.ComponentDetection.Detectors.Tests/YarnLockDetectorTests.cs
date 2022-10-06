using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponentNS;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class YarnLockDetectorTests
    {
        private Mock<ILogger> loggerMock;
        private ComponentRecorder componentRecorder;
        private DetectorTestUtility<YarnLockComponentDetector> detectorTestUtility;

        [TestInitialize]
        public void TestInitialize()
        {
            this.loggerMock = new Mock<ILogger>();
            this.componentRecorder = new ComponentRecorder();
            this.detectorTestUtility = DetectorTestUtilityCreator.Create<YarnLockComponentDetector>();
        }

        [TestMethod]
        public async Task WellFormedYarnLockV1WithZeroComponents_FindsNothing()
        {
            var yarnLock = YarnTestUtilities.GetWellFormedEmptyYarnV1LockFile();
            var packageJson = NpmTestUtilities.GetPackageJsonNoDependencies();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJson, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task WellFormedYarnLockV2WithZeroComponents_FindsNothing()
        {
            var yarnLock = YarnTestUtilities.GetWellFormedEmptyYarnV2LockFile();
            var packageJson = NpmTestUtilities.GetPackageJsonNoDependencies();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJson, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task MalformedYarnLockV1WithOneComponent_FindsNoComponent()
        {
            var componentName0 = Guid.NewGuid().ToString("N");
            var version0 = NewRandomVersion();
            var providedVersion0 = $"^{version0}";
            var resolved0 = "https://resolved0/a/resolved";

            var builder = new StringBuilder();

            builder.AppendLine("# THIS IS A YARNFILE");
            builder.AppendLine("# yarn lockfile v1");
            builder.AppendLine();
            builder.AppendLine($"{componentName0}@{providedVersion0}");
            builder.AppendLine($"  version {version0}");
            builder.AppendLine($"  resolved {resolved0}");

            var yarnLock = builder.ToString();
            var (packageJsonName, packageJsonContent, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, providedVersion0);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task MalformedYarnLockV2WithOneComponent_FindsNoComponent()
        {
            var componentName0 = Guid.NewGuid().ToString("N");
            var version0 = NewRandomVersion();
            var providedVersion0 = $"^{version0}";
            var resolved0 = "https://resolved0/a/resolved";

            var builder = new StringBuilder();

            builder.AppendLine(this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition>()));
            builder.AppendLine($"{componentName0}@{providedVersion0}");
            builder.AppendLine($"  version {version0}");
            builder.AppendLine($"  resolved {resolved0}");

            var yarnLock = builder.ToString();
            var (packageJsonName, packageJsonContent, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, providedVersion0);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task WellFormedYarnLockV1WithOneComponent_FindsComponent()
        {
            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = version0,
                Name = Guid.NewGuid().ToString("N"),
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "https://resolved0/a/resolved",
            };

            var yarnLock = this.CreateYarnLockV1FileContent(new List<YarnTestComponentDefinition> { componentA });
            var (packageJsonName, packageJsonContent, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentA.Name, componentA.RequestedVersion);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(componentA.Name, ((NpmComponent)detectedComponents.Single().Component).Name);
            Assert.AreEqual(version0, ((NpmComponent)detectedComponents.Single().Component).Version);

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                detectedComponents.Single().Component.Id,
                parentComponent => parentComponent.Name == componentA.Name && parentComponent.Version == version0);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV2WithOneComponent_FindsComponent()
        {
            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = version0,
                Name = Guid.NewGuid().ToString("N"),
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "https://resolved0/a/resolved",
            };

            var yarnLock = this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition> { componentA });
            var (packageJsonName, packageJsonContent, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentA.Name, componentA.RequestedVersion);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(componentA.Name, ((NpmComponent)detectedComponents.Single().Component).Name);
            Assert.AreEqual(version0, ((NpmComponent)detectedComponents.Single().Component).Version);

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                detectedComponents.Single().Component.Id,
                parentComponent => parentComponent.Name == componentA.Name && parentComponent.Version == version0);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV1WithWorkspace_FindsComponent()
        {
            var directory = new DirectoryInfo(Path.GetTempPath());

            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = version0,
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "https://resolved0/a/resolved",
                Name = Guid.NewGuid().ToString("N"),
            };

            var componentStream = YarnTestUtilities.GetMockedYarnLockStream("yarn.lock", this.CreateYarnLockV1FileContent(new List<YarnTestComponentDefinition> { componentA }));

            var workspaceJson = new
            {
                name = "testworkspace",
                version = "1.0.0",
                @private = true,
                workspaces = new[] { "workspace" },
            };

            var workspaceJsonComponentStream = new ComponentStream { Location = directory.ToString(), Pattern = "package.json", Stream = JsonConvert.SerializeObject(workspaceJson).ToStream() };

            var packageStream = NpmTestUtilities.GetPackageJsonOneRootComponentStream(componentA.Name, componentA.RequestedVersion);

            var detector = new YarnLockComponentDetector
            {
                Logger = this.loggerMock.Object,
            };

            var mock = new Mock<IComponentStreamEnumerableFactory>();

            // The initial call to get the yarn.lock file
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("yarn.lock")), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>())).Returns(new List<IComponentStream> { componentStream });

            // The call to get the package.json that contains workspace information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("package.json")), It.IsAny<ExcludeDirectoryPredicate>(), false)).Returns(new List<IComponentStream> { workspaceJsonComponentStream });

            // The call to get the package.json that contains actual information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.IsAny<Func<FileInfo, bool>>(), It.IsAny<ExcludeDirectoryPredicate>(), true)).Returns(new List<IComponentStream> { packageStream });

            var directoryWalkerMock = NpmTestUtilities.GetMockDirectoryWalker(new List<IComponentStream> { componentStream }, new List<IComponentStream> { packageStream }, directory.FullName, patterns: detector.SearchPatterns, componentRecorder: this.componentRecorder);

            var (scanResult, _) = await this.detectorTestUtility
                                                    .WithObservableDirectoryWalkerFactory(directoryWalkerMock)
                                                    .WithComponentStreamEnumerableFactory(mock)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = this.componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(componentA.Name, ((NpmComponent)detectedComponents.Single().Component).Name);
            Assert.AreEqual(version0, ((NpmComponent)detectedComponents.Single().Component).Version);

            this.componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                detectedComponents.Single().Component.Id,
                parentComponent => parentComponent.Name == componentA.Name && parentComponent.Version == version0);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV2WithWorkspace_FindsComponent()
        {
            var directory = new DirectoryInfo(Path.GetTempPath());

            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = version0,
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "https://resolved0/a/resolved",
                Name = Guid.NewGuid().ToString("N"),
            };

            var componentStream = YarnTestUtilities.GetMockedYarnLockStream("yarn.lock", this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition> { componentA }));

            var workspaceJson = new
            {
                name = "testworkspace",
                version = "1.0.0",
                @private = true,
                workspaces = new[] { "workspace" },
            };

            var workspaceJsonComponentStream = new ComponentStream { Location = directory.ToString(), Pattern = "package.json", Stream = JsonConvert.SerializeObject(workspaceJson).ToStream() };

            var packageStream = NpmTestUtilities.GetPackageJsonOneRootComponentStream(componentA.Name, componentA.RequestedVersion);

            var detector = new YarnLockComponentDetector
            {
                Logger = this.loggerMock.Object,
            };

            var mock = new Mock<IComponentStreamEnumerableFactory>();

            // The initial call to get the yarn.lock file
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("yarn.lock")), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>())).Returns(new List<IComponentStream> { componentStream });

            // The call to get the package.json that contains workspace information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("package.json")), It.IsAny<ExcludeDirectoryPredicate>(), false)).Returns(new List<IComponentStream> { workspaceJsonComponentStream });

            // The call to get the package.json that contains actual information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.IsAny<Func<FileInfo, bool>>(), It.IsAny<ExcludeDirectoryPredicate>(), true)).Returns(new List<IComponentStream> { packageStream });

            var directoryWalkerMock = NpmTestUtilities.GetMockDirectoryWalker(new List<IComponentStream> { componentStream }, new List<IComponentStream> { packageStream }, directory.FullName, patterns: detector.SearchPatterns, componentRecorder: this.componentRecorder);

            var (scanResult, _) = await this.detectorTestUtility
                                                    .WithObservableDirectoryWalkerFactory(directoryWalkerMock)
                                                    .WithComponentStreamEnumerableFactory(mock)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = this.componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(componentA.Name, ((NpmComponent)detectedComponents.Single().Component).Name);
            Assert.AreEqual(version0, ((NpmComponent)detectedComponents.Single().Component).Version);

            this.componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                detectedComponents.Single().Component.Id,
                parentComponent => parentComponent.Name == componentA.Name && parentComponent.Version == version0);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV1WithWorkspaceAltForm_FindsComponent()
        {
            var directory = new DirectoryInfo(Path.GetTempPath());

            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = $"\"{version0}\"",
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "\"https://resolved0/a/resolved\"",
                Name = Guid.NewGuid().ToString("N"),
            };

            var componentStream = YarnTestUtilities.GetMockedYarnLockStream("yarn.lock", this.CreateYarnLockV1FileContent(new List<YarnTestComponentDefinition> { componentA }));

            var workspaceJson = new
            {
                name = "testworkspace",
                version = "1.0.0",
                @private = true,
                workspaces = new { packages = new[] { "workspace" } },
            };

            var workspaceJsonComponentStream = new ComponentStream { Location = directory.ToString(), Pattern = "package.json", Stream = JsonConvert.SerializeObject(workspaceJson).ToStream() };

            var packageStream = NpmTestUtilities.GetPackageJsonOneRootComponentStream(componentA.Name, componentA.RequestedVersion);

            var detector = new YarnLockComponentDetector
            {
                Logger = this.loggerMock.Object,
            };

            var mock = new Mock<IComponentStreamEnumerableFactory>();

            // The initial call to get the yarn.lock file
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("yarn.lock")), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>())).Returns(new List<IComponentStream> { componentStream });

            // The call to get the package.json that contains workspace information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("package.json")), It.IsAny<ExcludeDirectoryPredicate>(), false)).Returns(new List<IComponentStream> { workspaceJsonComponentStream });

            // The call to get the package.json that contains actual information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.IsAny<Func<FileInfo, bool>>(), It.IsAny<ExcludeDirectoryPredicate>(), true)).Returns(new List<IComponentStream> { packageStream });

            var directoryWalkerMock = new Mock<IObservableDirectoryWalkerFactory>();

            directoryWalkerMock.Setup(walker => walker.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IComponentRecorder>()))
                .Returns(() => new IComponentStream[]
                    {
                        componentStream,
                        workspaceJsonComponentStream,
                        packageStream,
                    }.Select(cs => new ProcessRequest
                    {
                        ComponentStream = cs,
                        SingleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(cs.Location),
                    }).ToObservable());

            var (scanResult, _) = await this.detectorTestUtility
                                                    .WithObservableDirectoryWalkerFactory(directoryWalkerMock)
                                                    .WithComponentStreamEnumerableFactory(mock)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = this.componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(componentA.Name, ((NpmComponent)detectedComponents.Single().Component).Name);
            Assert.AreEqual(version0, ((NpmComponent)detectedComponents.Single().Component).Version);

            this.componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                detectedComponents.Single().Component.Id,
                parentComponent => parentComponent.Name == componentA.Name && parentComponent.Version == version0);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV2WithWorkspaceAltForm_FindsComponent()
        {
            var directory = new DirectoryInfo(Path.GetTempPath());

            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = $"\"{version0}\"",
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "\"https://resolved0/a/resolved\"",
                Name = Guid.NewGuid().ToString("N"),
            };

            var componentStream = YarnTestUtilities.GetMockedYarnLockStream("yarn.lock", this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition> { componentA }));

            var workspaceJson = new
            {
                name = "testworkspace",
                version = "1.0.0",
                @private = true,
                workspaces = new { packages = new[] { "workspace" } },
            };

            var workspaceJsonComponentStream = new ComponentStream { Location = directory.ToString(), Pattern = "package.json", Stream = JsonConvert.SerializeObject(workspaceJson).ToStream() };

            var packageStream = NpmTestUtilities.GetPackageJsonOneRootComponentStream(componentA.Name, componentA.RequestedVersion);

            var detector = new YarnLockComponentDetector
            {
                Logger = this.loggerMock.Object,
            };

            var mock = new Mock<IComponentStreamEnumerableFactory>();

            // The initial call to get the yarn.lock file
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("yarn.lock")), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>())).Returns(new List<IComponentStream> { componentStream });

            // The call to get the package.json that contains workspace information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.Is<IEnumerable<string>>(f => f.Contains("package.json")), It.IsAny<ExcludeDirectoryPredicate>(), false)).Returns(new List<IComponentStream> { workspaceJsonComponentStream });

            // The call to get the package.json that contains actual information
            mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), It.IsAny<Func<FileInfo, bool>>(), It.IsAny<ExcludeDirectoryPredicate>(), true)).Returns(new List<IComponentStream> { packageStream });

            var directoryWalkerMock = new Mock<IObservableDirectoryWalkerFactory>();

            directoryWalkerMock.Setup(walker => walker.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IComponentRecorder>()))
                .Returns(() => new IComponentStream[]
                    {
                        componentStream,
                        workspaceJsonComponentStream,
                        packageStream,
                    }.Select(cs => new ProcessRequest
                    {
                        ComponentStream = cs,
                        SingleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(cs.Location),
                    }).ToObservable());

            var (scanResult, _) = await this.detectorTestUtility
                                                    .WithObservableDirectoryWalkerFactory(directoryWalkerMock)
                                                    .WithComponentStreamEnumerableFactory(mock)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = this.componentRecorder.GetDetectedComponents();
            Assert.AreEqual(1, detectedComponents.Count());
            Assert.AreEqual(componentA.Name, ((NpmComponent)detectedComponents.Single().Component).Name);
            Assert.AreEqual(version0, ((NpmComponent)detectedComponents.Single().Component).Version);

            this.componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                detectedComponents.Single().Component.Id,
                parentComponent => parentComponent.Name == componentA.Name && parentComponent.Version == version0);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV1WithMoreThanOneComponent_FindsComponents()
        {
            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = version0,
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "https://resolved0/a/resolved",
                Name = Guid.NewGuid().ToString("N"),
            };

            var version1 = NewRandomVersion();
            var componentB = new YarnTestComponentDefinition
            {
                ActualVersion = version1,
                RequestedVersion = version1,
                ResolvedVersion = "https://resolved1/a/resolved",
                Name = Guid.NewGuid().ToString("N"),
            };

            componentA.Dependencies = new List<(string, string)> { (componentB.Name, componentB.RequestedVersion) };

            var yarnLock = this.CreateYarnLockV1FileContent(new List<YarnTestComponentDefinition> { componentA, componentB });
            var (packageJsonName, packageJsonContent, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentA.Name, componentA.RequestedVersion);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var component0 = detectedComponents.Select(x => x.Component).Cast<NpmComponent>().Single(x => x.Name == componentA.Name);
            var component1 = detectedComponents.Select(x => x.Component).Cast<NpmComponent>().Single(x => x.Name == componentB.Name);

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, detectedComponents.Count());

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component0.Id,
                parentComponent => parentComponent.Id == component0.Id);

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component1.Id,
                parentComponent => parentComponent.Id == component0.Id);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV2WithMoreThanOneComponent_FindsComponents()
        {
            var version0 = NewRandomVersion();
            var componentA = new YarnTestComponentDefinition
            {
                ActualVersion = version0,
                RequestedVersion = $"^{version0}",
                ResolvedVersion = "https://resolved0/a/resolved",
                Name = Guid.NewGuid().ToString("N"),
            };

            var version1 = NewRandomVersion();
            var componentB = new YarnTestComponentDefinition
            {
                ActualVersion = version1,
                RequestedVersion = version1,
                ResolvedVersion = "https://resolved1/a/resolved",
                Name = Guid.NewGuid().ToString("N"),
            };

            componentA.Dependencies = new List<(string, string)> { (componentB.Name, componentB.RequestedVersion) };

            var yarnLock = this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition> { componentA, componentB });
            var (packageJsonName, packageJsonContent, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentA.Name, componentA.RequestedVersion);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var component0 = detectedComponents.Select(x => x.Component).Cast<NpmComponent>().Single(x => x.Name == componentA.Name);
            var component1 = detectedComponents.Select(x => x.Component).Cast<NpmComponent>().Single(x => x.Name == componentB.Name);

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, detectedComponents.Count());

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component0.Id,
                parentComponent => parentComponent.Id == component0.Id);

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component1.Id,
                parentComponent => parentComponent.Id == component0.Id);
        }

        [TestMethod]
        public async Task WellFormedYarnLockV1WithMultiRootedComponent_FindsAllRoots()
        {
            // This is a regression test for a bug where a dependency is both an explicitly referenced root and a transitive dependency.
            //
            // component A is a root dependency
            // component B is a root devDependency
            // component A depends on component B
            //
            // we expect A to be detected as a non-dev dependency with roots [A]
            // we expect B to be detected as a non-dev dependency with roots [A, B]
            var componentNameA = "component-a";
            var actualVersionA = "1.1.1";
            var requestedVersionA = $"^{actualVersionA}";
            var resolvedA = "https://resolved0/a/resolved";

            var componentNameB = "root-dev-dependency-component";
            var actualVersionB = "2.2.2";
            var resolvedB = "https://resolved1/a/resolved";
            var requestedVersionB1 = $"^{actualVersionB}";
            var requestedVersionB2 = $"~{actualVersionB}";

            var packageJsonContent = $@"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{componentNameA}"": ""{requestedVersionA}""
                }},
                ""devDependencies"": {{
                    ""{componentNameB}"": ""{requestedVersionB1}""
                }}
            }}";
            var packageJson = packageJsonContent;

            var builder = new StringBuilder();

            builder.AppendLine("# THIS IS A YARNFILE");
            builder.AppendLine("# yarn lockfile v1");
            builder.AppendLine();
            builder.AppendLine($"{componentNameA}@{requestedVersionA}:");
            builder.AppendLine($"  version \"{actualVersionA}\"");
            builder.AppendLine($"  resolved \"{resolvedA}\"");
            builder.AppendLine($"  dependencies:");
            builder.AppendLine($"    {componentNameB} \"{requestedVersionB2}\"");
            builder.AppendLine();
            builder.AppendLine($"{componentNameB}@{requestedVersionB1}, {componentNameB}@{requestedVersionB2}:");
            builder.AppendLine($"  version \"{actualVersionB}\"");
            builder.AppendLine($"  resolved \"{resolvedB}\"");
            builder.AppendLine();

            var yarnLock = builder.ToString();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            var detectedComponentes = componentRecorder.GetDetectedComponents();
            var componentA = detectedComponentes.Single(x => ((NpmComponent)x.Component).Name == componentNameA);
            var componentB = detectedComponentes.Single(x => ((NpmComponent)x.Component).Name == componentNameB);

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, detectedComponentes.Count());

            // Component A
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                componentA.Component.Id,
                parentComponent => parentComponent.Id == componentA.Component.Id);
            Assert.AreEqual(false, componentRecorder.GetEffectiveDevDependencyValue(componentA.Component.Id));

            // Component B
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                componentB.Component.Id,
                parentComponent1 => parentComponent1.Id == componentA.Component.Id,
                parentComponent2 => parentComponent2.Id == componentB.Component.Id);
            Assert.AreEqual(false, componentRecorder.GetEffectiveDevDependencyValue(componentB.Component.Id));
        }

        [TestMethod]
        public async Task WellFormedYarnLockV2WithMultiRootedComponent_FindsAllRoots()
        {
            // This is a regression test for a bug where a dependency is both an explicitly referenced root and a transitive dependency.
            //
            // component A is a root dependency
            // component B is a root devDependency
            // component A depends on component B
            //
            // we expect A to be detected as a non-dev dependency with roots [A]
            // we expect B to be detected as a non-dev dependency with roots [A, B]
            var componentNameA = "component-a";
            var actualVersionA = "1.1.1";
            var requestedVersionA = $"^{actualVersionA}";
            var resolvedA = "https://resolved0/a/resolved";

            var componentNameB = "root-dev-dependency-component";
            var actualVersionB = "2.2.2";
            var resolvedB = "https://resolved1/a/resolved";
            var requestedVersionB1 = $"^{actualVersionB}";
            var requestedVersionB2 = $"~{actualVersionB}";

            var packageJsonContent = $@"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{componentNameA}"": ""{requestedVersionA}""
                }},
                ""devDependencies"": {{
                    ""{componentNameB}"": ""{requestedVersionB1}""
                }}
            }}";
            var packageJson = packageJsonContent;

            var builder = new StringBuilder();

            builder.AppendLine(this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition>()));
            builder.AppendLine($"{componentNameA}@{requestedVersionA}:");
            builder.AppendLine($"  version: {actualVersionA}");
            builder.AppendLine($"  resolved: {resolvedA}");
            builder.AppendLine($"  dependencies:");
            builder.AppendLine($"    {componentNameB}: {requestedVersionB2}");
            builder.AppendLine();
            builder.AppendLine($"{componentNameB}@{requestedVersionB1}, {componentNameB}@{requestedVersionB2}:");
            builder.AppendLine($"  version: {actualVersionB}");
            builder.AppendLine($"  resolved: {resolvedB}");
            builder.AppendLine();

            var yarnLock = builder.ToString();

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLock)
                                                    .WithFile("package.json", packageJsonContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            var detectedComponentes = componentRecorder.GetDetectedComponents();
            var componentA = detectedComponentes.Single(x => ((NpmComponent)x.Component).Name == componentNameA);
            var componentB = detectedComponentes.Single(x => ((NpmComponent)x.Component).Name == componentNameB);

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, detectedComponentes.Count());

            // Component A
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                componentA.Component.Id,
                parentComponent => parentComponent.Id == componentA.Component.Id);
            Assert.AreEqual(false, componentRecorder.GetEffectiveDevDependencyValue(componentA.Component.Id));

            // Component B
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                componentB.Component.Id,
                parentComponent1 => parentComponent1.Id == componentA.Component.Id,
                parentComponent2 => parentComponent2.Id == componentB.Component.Id);
            Assert.AreEqual(false, componentRecorder.GetEffectiveDevDependencyValue(componentB.Component.Id));
        }

        [TestMethod]
        public async Task DependencyGraphV1IsGeneratedCorrectly()
        {
            var componentA = new YarnTestComponentDefinition
            {
                Name = "component-a",
                ActualVersion = "1.1.1",
                RequestedVersion = "^1.1.1",
                ResolvedVersion = "https://resolved0/b/resolved",
            };

            var componentB = new YarnTestComponentDefinition
            {
                Name = "component-b",
                ActualVersion = "1.1.1",
                RequestedVersion = "^1.1.1",
                ResolvedVersion = "https://resolved0/c/resolved",
            };

            var componentC = new YarnTestComponentDefinition
            {
                Name = "component-c",
                ActualVersion = "1.1.1",
                RequestedVersion = "^1.1.1",
                ResolvedVersion = "https://resolved0/d/resolved",
            };

            componentA.Dependencies = new List<(string, string)> { (componentB.Name, componentB.RequestedVersion) };
            componentB.Dependencies = new List<(string, string)> { (componentC.Name, componentC.RequestedVersion) };

            var yarnLockFileContent = this.CreateYarnLockV1FileContent(new List<YarnTestComponentDefinition> { componentA, componentB, componentC });
            var packageJsonFileContent = this.CreatePackageJsonFileContent(new List<YarnTestComponentDefinition> { componentA, componentB, componentC });

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLockFileContent)
                                                    .WithFile("package.json", packageJsonFileContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name == componentA.Name).Component.Id;
            var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name == componentB.Name).Component.Id;
            var componentCId = detectedComponents.First(c => ((NpmComponent)c.Component).Name == componentC.Name).Component.Id;

            var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

            dependencyGraph.GetDependenciesForComponent(componentAId).Should().HaveCount(1);
            dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);

            dependencyGraph.GetDependenciesForComponent(componentBId).Should().HaveCount(1);
            dependencyGraph.GetDependenciesForComponent(componentBId).Should().Contain(componentCId);

            dependencyGraph.GetDependenciesForComponent(componentCId).Should().HaveCount(0);
        }

        [TestMethod]
        public async Task DependencyGraphV2IsGeneratedCorrectly()
        {
            var componentA = new YarnTestComponentDefinition
            {
                Name = "component-a",
                ActualVersion = "1.1.1",
                RequestedVersion = "^1.1.1",
                ResolvedVersion = "https://resolved0/b/resolved",
            };

            var componentB = new YarnTestComponentDefinition
            {
                Name = "component-b",
                ActualVersion = "1.1.1",
                RequestedVersion = "^1.1.1",
                ResolvedVersion = "https://resolved0/c/resolved",
            };

            var componentC = new YarnTestComponentDefinition
            {
                Name = "component-c",
                ActualVersion = "1.1.1",
                RequestedVersion = "^1.1.1",
                ResolvedVersion = "https://resolved0/d/resolved",
            };

            componentA.Dependencies = new List<(string, string)> { (componentB.Name, componentB.RequestedVersion) };
            componentB.Dependencies = new List<(string, string)> { (componentC.Name, componentC.RequestedVersion) };

            var yarnLockFileContent = this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition> { componentA, componentB, componentC });
            var packageJsonFileContent = this.CreatePackageJsonFileContent(new List<YarnTestComponentDefinition> { componentA, componentB, componentC });

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                                                    .WithFile("yarn.lock", yarnLockFileContent)
                                                    .WithFile("package.json", packageJsonFileContent, new List<string> { "package.json" })
                                                    .ExecuteDetector();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name == componentA.Name).Component.Id;
            var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name == componentB.Name).Component.Id;
            var componentCId = detectedComponents.First(c => ((NpmComponent)c.Component).Name == componentC.Name).Component.Id;

            var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

            dependencyGraph.GetDependenciesForComponent(componentAId).Should().HaveCount(1);
            dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);

            dependencyGraph.GetDependenciesForComponent(componentBId).Should().HaveCount(1);
            dependencyGraph.GetDependenciesForComponent(componentBId).Should().Contain(componentCId);

            dependencyGraph.GetDependenciesForComponent(componentCId).Should().HaveCount(0);
        }

        [TestMethod]
        public async Task MalformedYarnLockV1_Duplicate()
        {
            const string componentNameA = "lodash-shim";
            const string requestedVersionA = "file:lodash-shim";
            const string componentNameB = "lodash";
            const string requestedVersionB = "file:lodash-shim";
            const string actualVersion = "2.4.2";

            var builder = new StringBuilder();

            builder.AppendLine(this.CreateYarnLockV2FileContent(new List<YarnTestComponentDefinition>()));
            builder.AppendLine($"\"{componentNameA}@{requestedVersionA}\", \"{componentNameB}@{requestedVersionB}\":");
            builder.AppendLine($"  version: {actualVersion}");
            builder.AppendLine();

            var yarnLockFileContent = builder.ToString();
            var packageJsonFileContent = this.CreatePackageJsonFileContent(new List<YarnTestComponentDefinition>());

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithFile("yarn.lock", yarnLockFileContent)
                .WithFile("package.json", packageJsonFileContent, new List<string> { "package.json" })
                .ExecuteDetector();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var detectedComponents = componentRecorder.GetDetectedComponents();

            detectedComponents.Should().HaveCount(1);
            var detectedComponent = detectedComponents.First();
        }

        private string CreatePackageJsonFileContent(IList<YarnTestComponentDefinition> components)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            builder.Append(@"""name"": ""test"",");
            builder.Append(@"""version"": ""0.0.0"",");
            builder.Append(@"""dependencies"": {");

            var prodComponents = components.Where(c => !c.IsDevDependency).ToList();
            for (var i = 0; i < prodComponents.Count; i++)
            {
                if (i == prodComponents.Count - 1)
                {
                    builder.Append($@"  ""{prodComponents[i].Name}"": ""{prodComponents[i].RequestedVersion}""");
                }
                else
                {
                    builder.Append($@"  ""{prodComponents[i].Name}"": ""{prodComponents[i].RequestedVersion}"",");
                }
            }

            builder.Append("}");

            if (components.Any(component => component.IsDevDependency))
            {
                builder.Append(",");
                builder.Append(@"""devDependencies"": {");

                var dependencyComponents = components.Where(c => c.IsDevDependency).ToList();

                for (var i = 0; i < dependencyComponents.Count; i++)
                {
                    if (i == dependencyComponents.Count - 1)
                    {
                        builder.Append($@"  ""{dependencyComponents[i].Name}"": ""{dependencyComponents[i].RequestedVersion}""");
                    }
                    else
                    {
                        builder.Append($@"  ""{dependencyComponents[i].Name}"": ""{dependencyComponents[i].RequestedVersion}"",");
                    }
                }
            }

            builder.Append("}");
            builder.Append("}");

            return builder.ToString();
        }

        private string CreateYarnLockV1FileContent(IEnumerable<YarnTestComponentDefinition> components)
        {
            var builder = new StringBuilder();

            builder.AppendLine("# yarn lockfile v1");
            builder.AppendLine();

            foreach (var component in components)
            {
                builder.AppendLine($"{component.Name}@{component.RequestedVersion}:");
                builder.AppendLine($"  version \"{component.ActualVersion}\"");
                builder.AppendLine($"  resolved \"{component.ResolvedVersion}\"");

                if (component.Dependencies.Any())
                {
                    builder.AppendLine($"  dependencies:");
                    foreach (var dependency in component.Dependencies)
                    {
                        builder.AppendLine($"    {dependency.Name} \"{dependency.RequestedVersion}\"");
                    }
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private string CreateYarnLockV2FileContent(IEnumerable<YarnTestComponentDefinition> components)
        {
            var builder = new StringBuilder();

            builder.AppendLine("# This file is generated by running \"yarn install\" inside your project.");
            builder.AppendLine("# Manual changes might be lost - proceed with caution!");
            builder.AppendLine();
            builder.AppendLine("__metadata:");
            builder.AppendLine("  version: 4");
            builder.AppendLine("  cacheKey: 7");
            builder.AppendLine();

            foreach (var component in components)
            {
                builder.AppendLine($"{component.Name}@{component.RequestedVersion}:");
                builder.AppendLine($"  version: {component.ActualVersion}");
                builder.AppendLine($"  resolved: {component.ResolvedVersion}");

                if (component.Dependencies.Any())
                {
                    builder.AppendLine($"  dependencies:");
                    foreach (var dependency in component.Dependencies)
                    {
                        builder.AppendLine($"    {dependency.Name}: {dependency.RequestedVersion}");
                    }
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private class YarnTestComponentDefinition
        {
            public string Name { get; set; }

            public string RequestedVersion { get; set; }

            public string ActualVersion { get; set; }

            public string ResolvedVersion { get; set; }

            public bool IsDevDependency { get; set; }

            public IList<(string Name, string RequestedVersion)> Dependencies { get; set; } = new List<(string, string)>();
        }
    }
}

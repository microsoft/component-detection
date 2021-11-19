using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;

using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class NpmDetectorWithRootsTests
    {
        private Mock<ILogger> loggerMock;
        private Mock<IPathUtilityService> pathUtilityService;
        private ComponentRecorder componentRecorder;
        private DetectorTestUtility<NpmComponentDetectorWithRoots> detectorTestUtility = DetectorTestUtilityCreator.Create<NpmComponentDetectorWithRoots>();
        private string packageLockJsonFileName = "package-lock.json";
        private string packageJsonFileName = "package.json";
        private List<string> packageJsonSearchPattern = new List<string> { "package.json" };

        [TestInitialize]
        public void TestInitialize()
        {
            loggerMock = new Mock<ILogger>();
            pathUtilityService = new Mock<IPathUtilityService>();
            pathUtilityService.Setup(x => x.GetParentDirectory(It.IsAny<string>())).Returns((string path) => Path.GetDirectoryName(path));
            componentRecorder = new ComponentRecorder();
        }

        [TestMethod]
        public async Task TestNpmDetector_PackageLockReturnsValid()
        {
            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();

            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName, componentName0, version0);
            var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns, fileLocation: packageLockPath)
                                                    .WithFile(packageJsonName, packageJsonContents, packageJsonSearchPattern, fileLocation: packageJsonPath)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());
            foreach (var component in detectedComponents)
            {
                componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(((NpmComponent)component.Component).Hash));
            }
        }

        [TestMethod]
        public async Task TestNpmDetector_MismatchedFilesReturnsEmpty()
        {
            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();

            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName);
            var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns, fileLocation: packageLockPath)
                                                    .WithFile(packageJsonName, packageJsonContents, packageJsonSearchPattern, fileLocation: packageJsonPath)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNpmDetector_MissingPackageJsonReturnsEmpty()
        {
            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns, fileLocation: packageLockPath)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNpmDetector_PackageLockMultiRoot()
        {
            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName1 = Guid.NewGuid().ToString("N");
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();
            string componentName3 = Guid.NewGuid().ToString("N");

            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName, componentName0, version0, componentName2, version2, packageName1: componentName1, packageName3: componentName3);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());

            var component0 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName0));

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component0.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0);

            var component1 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName1));
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component1.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0);

            var component2 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName2));

            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component2.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0,
                    parentComponent2 => parentComponent2.Name == componentName2);

            var component3 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName3));
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component3.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0,
                    parentComponent2 => parentComponent2.Name == componentName2);
        }

        [TestMethod]
        public async Task TestNpmDetector_VerifyMultiRoot_DependencyGraph()
        {
            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();

            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName, componentName0, version0, componentName2, version2);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns, fileLocation: packageLockPath)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            var graphsByLocation = componentRecorder.GetDependencyGraphsByLocation();

            var graph = graphsByLocation[packageLockPath];

            var npmComponent0Id = new NpmComponent(componentName0, version0).Id;
            var npmComponent2Id = new NpmComponent(componentName2, version2).Id;

            var dependenciesFor0 = graph.GetDependenciesForComponent(npmComponent0Id);
            Assert.AreEqual(dependenciesFor0.Count(), 2);
            var dependenciesFor2 = graph.GetDependenciesForComponent(npmComponent2Id);
            Assert.AreEqual(dependenciesFor2.Count(), 1);

            Assert.IsTrue(dependenciesFor0.Contains(npmComponent2Id));
        }

        [TestMethod]
        public async Task TestNpmDetector_EmptyVersionSkipped()
        {
            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();

            string packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": """",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

            var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockJsonFileName, packageLockTemplate, detector.SearchPatterns)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNpmDetector_InvalidNameSkipped()
        {
            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();

            string packageLockJson = @"{{
                ""name"": """",
                ""version"": ""1.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

            var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockJsonFileName, packageLockTemplate, detector.SearchPatterns)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNpmDetector_LernaDirectory()
        {
            string lockFileLocation = Path.Combine(Path.GetTempPath(), Path.Combine("belowLerna", packageLockJsonFileName));
            string packageJsonFileLocation = Path.Combine(Path.GetTempPath(), Path.Combine("belowLerna", packageJsonFileName));
            string lernaFileLocation = Path.Combine(Path.GetTempPath(), "lerna.json");

            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName1 = Guid.NewGuid().ToString("N");
            string version1 = NewRandomVersion();
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();

            string packageLockJson = @"{{
                ""name"": """",
                ""version"": """",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

            var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}"",
                    ""{4}"": ""{5}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1, componentName2, version2);

            var detector = new NpmComponentDetectorWithRoots();
            pathUtilityService.Setup(x => x.IsFileBelowAnother(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                        .WithDetector(detector)
                                        .WithFile("lerna.json", "unused string", detector.SearchPatterns, fileLocation: lernaFileLocation)
                                        .WithFile(packageLockJsonFileName, packageLockTemplate, detector.SearchPatterns, fileLocation: lockFileLocation)
                                        .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern, fileLocation: packageJsonFileLocation)
                                        .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
            Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());
        }

        [TestMethod]
        public async Task TestNpmDetector_CircularRequirementsResolve()
        {
            string packageJsonComponentPath = Path.Combine(Path.GetTempPath(), packageLockJsonFileName);

            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();

            string packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

            var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockJsonFileName, packageLockTemplate, detector.SearchPatterns)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(2, detectedComponents.Count());

            foreach (var component in detectedComponents)
            {
                componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0,
                    parentComponent2 => parentComponent2.Name == componentName2);
            }
        }

        [TestMethod]
        public async Task TestNpmDetector_ShrinkwrapLockReturnsValid()
        {
            string lockFileName = "npm-shrinkwrap.json";
            string packageJsonComponentPath = Path.Combine(Path.GetTempPath(), packageJsonFileName);

            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();

            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(lockFileName, componentName0, version0);
            var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns, fileLocation: packageLockPath)
                                                    .WithFile(packageJsonFileName, packageJsonContents, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());
            foreach (var component in detectedComponents)
            {
                componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
            }
        }

        [TestMethod]
        public async Task TestNpmDetector_IgnoresPackageLocksInSubFolders()
        {
            string pathRoot = Path.GetTempPath();

            string packageLockUnderNodeModules = Path.Combine(pathRoot, Path.Combine("node_modules", packageLockJsonFileName));
            string packageJsonUnderNodeModules = Path.Combine(pathRoot, Path.Combine("node_modules", packageJsonFileName));

            string componentName0 = Guid.NewGuid().ToString("N");
            string version0 = NewRandomVersion();
            string componentName2 = Guid.NewGuid().ToString("N");
            string version2 = NewRandomVersion();

            var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName, componentName0, version0);
            var (packageLockName2, packageLockContents2, packageLockPath2) = NpmTestUtilities.GetWellFormedPackageLock2(packageLockJsonFileName, componentName2, version2, packageName0: "test2");

            string packagejson = @"{{
                ""name"": ""{2}"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentName0, version0, "test");

            var packageJsonTemplate2 = string.Format(packagejson, componentName2, version2, "test2");

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    /* Top level */
                                                    .WithFile(packageLockName, packageLockContents, detector.SearchPatterns, fileLocation: packageLockPath)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    /* Under node_modules */
                                                    .WithFile(packageLockName2, packageLockContents2, detector.SearchPatterns, fileLocation: packageLockUnderNodeModules)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate2, packageJsonSearchPattern, fileLocation: packageJsonUnderNodeModules)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            Assert.AreEqual(4, detectedComponents.Count());
            foreach (var component in detectedComponents)
            {
                componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                    component.Component.Id,
                    parentComponent0 => parentComponent0.Name == componentName0);
            }
        }

        [TestMethod]
        public async Task TestNpmDetector_DependencyGraphIsCreated()
        {
            string packageJsonComponentPath = Path.Combine(Path.GetTempPath(), packageLockJsonFileName);

            var componentA = (Name: "componentA", Version: "1.0.0");
            var componentB = (Name: "componentB", Version: "1.0.0");
            var componentC = (Name: "componentC", Version: "1.0.0");

            string packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""dependencies"": {{
                            ""{6}"": {{
                                ""version"": ""{7}"",
                                ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                                ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                            }}
                        }}
                    }}
                }}
            }}";

            var packageLockTemplate = string.Format(
                packageLockJson,
                componentA.Name, componentA.Version,
                componentB.Name, componentB.Version,
                componentB.Name, componentB.Version,
                componentC.Name, componentC.Version);

            string packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

            var packageJsonTemplate = string.Format(packagejson, componentA.Name, componentA.Version, componentB.Name, componentB.Version);

            var detector = new NpmComponentDetectorWithRoots();
            detector.PathUtilityService = pathUtilityService.Object;

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithDetector(detector)
                                                    .WithFile(packageLockJsonFileName, packageLockTemplate, detector.SearchPatterns)
                                                    .WithFile(packageJsonFileName, packageJsonTemplate, packageJsonSearchPattern)
                                                    .ExecuteDetector();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            detectedComponents.Should().HaveCount(3);

            var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentA.Name)).Component.Id;
            var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentB.Name)).Component.Id;
            var componentCId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentC.Name)).Component.Id;

            var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

            dependencyGraph.GetDependenciesForComponent(componentAId).Should().HaveCount(1);
            dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);
            dependencyGraph.GetDependenciesForComponent(componentBId).Should().HaveCount(1);
            dependencyGraph.GetDependenciesForComponent(componentBId).Should().Contain(componentCId);
            dependencyGraph.GetDependenciesForComponent(componentCId).Should().HaveCount(0);
        }
    }
}

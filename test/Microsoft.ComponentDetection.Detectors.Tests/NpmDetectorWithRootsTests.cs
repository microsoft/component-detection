#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NpmDetectorWithRootsTests : BaseDetectorTest<NpmComponentDetectorWithRoots>
{
    private readonly string packageLockJsonFileName = "package-lock.json";
    private readonly string packageJsonFileName = "package.json";
    private readonly List<string> packageJsonSearchPattern = ["package.json"];
    private readonly List<string> packageLockJsonSearchPatterns = ["package-lock.json", "npm-shrinkwrap.json", "lerna.json"];
    private readonly Mock<IPathUtilityService> mockPathUtilityService;

    public NpmDetectorWithRootsTests()
    {
        this.mockPathUtilityService = new Mock<IPathUtilityService>();
        this.DetectorTestUtility.AddServiceMock(this.mockPathUtilityService);
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockReturnsValidAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockReturnsValidWhenDevAndOptionalDependenciesAsync()
    {
        var rootName = Guid.NewGuid().ToString("N");
        var rootVersion = NewRandomVersion();
        var devDepName = Guid.NewGuid().ToString("N");
        var devDepVersion = NewRandomVersion();
        var optDepName = Guid.NewGuid().ToString("N");
        var optDepVersion = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2WithOptionalAndDevDependency(this.packageLockJsonFileName, rootName, rootVersion, devDepName, devDepVersion, optDepName, optDepVersion);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRootOneDevDependencyOneOptionalDependency(rootName, rootVersion, devDepName, devDepVersion, optDepName, optDepVersion);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var retrievedDevDep = detectedComponents.Single(c => ((NpmComponent)c.Component).Name.Equals(devDepName));
        componentRecorder.GetEffectiveDevDependencyValue(retrievedDevDep.Component.Id).Should().BeTrue();

        var retrievedOptDep = detectedComponents.Single(c => ((NpmComponent)c.Component).Name.Equals(optDepName));
        componentRecorder.GetEffectiveDevDependencyValue(retrievedOptDep.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task TestNpmDetector_MismatchedFilesReturnsEmptyAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNpmDetector_MissingPackageJsonReturnsEmptyAsync()
    {
        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockMultiRootAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();
        var componentName3 = Guid.NewGuid().ToString("N");

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0, componentName2, version2, packageName1: componentName1, packageName3: componentName3);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);

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
    public async Task TestNpmDetector_VerifyMultiRoot_DependencyGraphAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0, componentName2, version2);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        var graphsByLocation = componentRecorder.GetDependencyGraphsByLocation();

        var graph = graphsByLocation[packageLockPath];

        var npmComponent0Id = new NpmComponent(componentName0, version0).Id;
        var npmComponent2Id = new NpmComponent(componentName2, version2).Id;

        var dependenciesFor0 = graph.GetDependenciesForComponent(npmComponent0Id);
        dependenciesFor0.Should().HaveCount(2);
        var dependenciesFor2 = graph.GetDependenciesForComponent(npmComponent2Id);
        dependenciesFor2.Should().ContainSingle();

        dependenciesFor0.Should().Contain(npmComponent2Id);
    }

    [TestMethod]
    public async Task TestNpmDetector_EmptyVersionSkippedAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
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

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNpmDetector_InvalidNameSkippedAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
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

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNpmDetector_LernaDirectoryAsync()
    {
        var lockFileLocation = Path.Combine(Path.GetTempPath(), Path.Combine("belowLerna", this.packageLockJsonFileName));
        var packageJsonFileLocation = Path.Combine(Path.GetTempPath(), Path.Combine("belowLerna", this.packageJsonFileName));
        var lernaFileLocation = Path.Combine(Path.GetTempPath(), "lerna.json");

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
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

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}"",
                    ""{4}"": ""{5}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1, componentName2, version2);

        this.mockPathUtilityService.Setup(x => x.IsFileBelowAnother(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("lerna.json", "unused string", this.packageLockJsonSearchPatterns, fileLocation: lernaFileLocation)
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns, fileLocation: lockFileLocation)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern, fileLocation: packageJsonFileLocation)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);
    }

    [TestMethod]
    public async Task TestNpmDetector_CircularRequirementsResolveAsync()
    {
        var packageJsonComponentPath = Path.Combine(Path.GetTempPath(), this.packageLockJsonFileName);

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
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

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0,
                parentComponent2 => parentComponent2.Name == componentName2);
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_ShrinkwrapLockReturnsValidAsync()
    {
        var lockFileName = "npm-shrinkwrap.json";
        var packageJsonComponentPath = Path.Combine(Path.GetTempPath(), this.packageJsonFileName);

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(lockFileName, componentName0, version0);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_IgnoresPackageLocksInSubFoldersAsync()
    {
        var pathRoot = Path.GetTempPath();

        var packageLockUnderNodeModules = Path.Combine(pathRoot, Path.Combine("node_modules", this.packageLockJsonFileName));
        var packageJsonUnderNodeModules = Path.Combine(pathRoot, Path.Combine("node_modules", this.packageJsonFileName));

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0);
        var (packageLockName2, packageLockContents2, packageLockPath2) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName2, version2, packageName0: "test2");

        var packagejson = @"{{
                ""name"": ""{2}"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, "test");

        var packageJsonTemplate2 = string.Format(packagejson, componentName2, version2, "test2");

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            /* Top level */
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            /* Under node_modules */
            .WithFile(packageLockName2, packageLockContents2, this.packageLockJsonSearchPatterns, fileLocation: packageLockUnderNodeModules)
            .WithFile(this.packageJsonFileName, packageJsonTemplate2, this.packageJsonSearchPattern, fileLocation: packageJsonUnderNodeModules)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0);
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_DependencyGraphIsCreatedAsync()
    {
        var packageJsonComponentPath = Path.Combine(Path.GetTempPath(), this.packageLockJsonFileName);

        var componentA = (Name: "componentA", Version: "1.0.0");
        var componentB = (Name: "componentB", Version: "1.0.0");
        var componentC = (Name: "componentC", Version: "1.0.0");

        var packageLockJson = @"{{
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
            componentA.Name,
            componentA.Version,
            componentB.Name,
            componentB.Version,
            componentB.Name,
            componentB.Version,
            componentC.Name,
            componentC.Version);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentA.Name, componentA.Version, componentB.Name, componentB.Version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentA.Name)).Component.Id;
        var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentB.Name)).Component.Id;
        var componentCId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentC.Name)).Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(componentAId).Should().ContainSingle();
        dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);
        dependencyGraph.GetDependenciesForComponent(componentBId).Should().ContainSingle();
        dependencyGraph.GetDependenciesForComponent(componentBId).Should().Contain(componentCId);
        dependencyGraph.GetDependenciesForComponent(componentCId).Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockWithoutDependenciesObject_ShouldHandleGracefully()
    {
        // This test reproduces the NullReferenceException issue when package-lock.json doesn't contain a "dependencies" object
        var packageLockJson = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""lockfileVersion"": 2
            }";

        var packageJsonContents = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0""
            }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockJson, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        // The detector should handle the missing "dependencies" object gracefully without throwing
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty(); // No dependencies should be detected
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockMissingDependenciesButPackageJsonHasDependencies_ShouldHandleGracefully()
    {
        // This test reproduces a more specific scenario where package.json has dependencies but package-lock.json is missing dependencies
        var packageLockJson = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""lockfileVersion"": 2
            }";

        var packageJsonContents = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""dependencies"": {
                    ""lodash"": ""^4.17.21""
                }
            }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockJson, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        // The detector should handle the missing "dependencies" object gracefully without throwing
        // This may result in processing failure but should not throw NullReferenceException
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty(); // No dependencies should be detected since dependencies is missing
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockMissingDependenciesProperty_ShouldNotThrowNullReferenceException()
    {
        // This test reproduces the exact NullReferenceException scenario from the issue:
        // package-lock.json doesn't contain a "dependencies" property at all
        var packageLockJson = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""lockfileVersion"": 2
            }";

        var packageJsonContents = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0""
            }";

        // Before the fix, this would throw a NullReferenceException because
        // packageLockJToken["dependencies"] returns null, and calling .Children<JProperty>() on null throws
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockJson, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        // The detector should handle the missing "dependencies" property gracefully without throwing
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty(); // No dependencies should be detected since dependencies is missing
    }
}

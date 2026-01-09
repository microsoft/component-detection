#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
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
public class NpmLockfile3DetectorTests : BaseDetectorTest<NpmLockfile3Detector>
{
    private readonly string packageLockJsonFileName = "package-lock.json";
    private readonly string packageJsonFileName = "package.json";
    private readonly List<string> packageJsonSearchPattern = ["package.json"];
    private readonly List<string> packageLockJsonSearchPatterns = ["package-lock.json", "npm-shrinkwrap.json", "lerna.json"];
    private readonly Mock<IPathUtilityService> mockPathUtilityService;

    public NpmLockfile3DetectorTests()
    {
        this.mockPathUtilityService = new Mock<IPathUtilityService>();
        this.DetectorTestUtility.AddServiceMock(this.mockPathUtilityService);
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3ReturnsValidAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock3(this.packageLockJsonFileName, componentName0, version0);
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
    public async Task TestNpmDetector_PackageLockVersion3NestedReturnsValidAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedNestedPackageLock3(this.packageLockJsonFileName, componentName0, version0, componentName1, version1, componentName2);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(4);

        var component0 = detectedComponents.First(x => x.Component.Id.Contains(componentName0));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component0.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0);

        var component1 = detectedComponents.First(x => x.Component.Id.Contains(componentName1));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component1.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName1);

        var duplicate = detectedComponents.Where(x => x.Component.Id.Contains(componentName2)).ToList();
        duplicate.Should().HaveCount(2);

        foreach (var component in detectedComponents)
        {
            // check that either component0 or component1 is our parent
            componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 || parentComponent0.Name == componentName1);
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithDevDependenciesReturnsValidAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedNestedPackageLock3WithDevDependencies(this.packageLockJsonFileName, componentName0, version0, componentName1, version1);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""devDependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(2);

        var component0 = detectedComponents.First(x => x.Component.Id.Contains(componentName0));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component0.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0);

        var component1 = detectedComponents.First(x => x.Component.Id.Contains(componentName1));
        componentRecorder.GetEffectiveDevDependencyValue(component0.Component.Id).Should().BeTrue();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component1.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName1);
        componentRecorder.GetEffectiveDevDependencyValue(component1.Component.Id).Should().BeTrue();

        foreach (var component in detectedComponents)
        {
            // check that either component0 or component1 is our parent
            componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 || parentComponent0.Name == componentName1);
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithOptionalDependenciesReturnsValidAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedNestedPackageLock3WithOptionalDependencies(this.packageLockJsonFileName, componentName0, version0, componentName1, version1);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""optionalDependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(2);

        var component0 = detectedComponents.First(x => x.Component.Id.Contains(componentName0));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component0.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0);

        var component1 = detectedComponents.First(x => x.Component.Id.Contains(componentName1));
        componentRecorder.GetEffectiveDevDependencyValue(component0.Component.Id).Should().BeFalse();
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component1.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName1);
        componentRecorder.GetEffectiveDevDependencyValue(component1.Component.Id).Should().BeFalse();

        foreach (var component in detectedComponents)
        {
            // check that either component0 or component1 is our parent
            componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 || parentComponent0.Name == componentName1);
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_NestedNodeModulesV3Async()
    {
        var componentA = (Name: "componentA", Version: "1.0.0");
        var componentB = (Name: "componentB", Version: "1.0.0");

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""requires"": true,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""dependencies"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""node_modules/{0}/node_modules/{2}"": {{
                        ""version"": ""{3}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentA.Name, componentA.Version, componentB.Name, componentB.Version);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentA.Name, componentA.Version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentA.Name)).Component.Id;
        var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentB.Name)).Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(componentAId).Should().ContainSingle();
        dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);
        dependencyGraph.GetDependenciesForComponent(componentBId).Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockWithoutPackagesObject_ShouldHandleGracefully()
    {
        // This test reproduces the NullReferenceException issue when package-lock.json doesn't contain a "packages" object
        var packageLockJson = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""lockfileVersion"": 3
            }";

        var packageJsonContents = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0""
            }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockJson, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        // The detector should handle the missing "packages" object gracefully without throwing
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty(); // No dependencies should be detected
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockMissingPackagesButPackageJsonHasDependencies_ShouldHandleGracefully()
    {
        // This test reproduces a more specific scenario where package.json has dependencies but package-lock.json is missing packages
        var packageLockJson = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""lockfileVersion"": 3
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

        // The detector should handle the missing "packages" object gracefully without throwing
        // This may result in processing failure but should not throw NullReferenceException
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty(); // No dependencies should be detected since packages is missing
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockMissingPackagesProperty_ShouldNotThrowNullReferenceException()
    {
        // This test reproduces the exact NullReferenceException scenario from the issue:
        // package-lock.json doesn't contain a "packages" property at all
        var packageLockJson = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0"",
                ""lockfileVersion"": 3
            }";

        var packageJsonContents = @"{
                ""name"": ""test"",
                ""version"": ""1.0.0""
            }";

        // Before the fix, this would throw a NullReferenceException because
        // packageLockJToken["packages"] returns null, and calling .Children<JProperty>() on null throws
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockJson, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        // The detector should handle the missing "packages" property gracefully without throwing
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty(); // No dependencies should be detected since packages is missing
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithDevOptionalDependenciesReturnsValidAsync()
    {
        // Test for issue #1380: devOptional dependencies (peer + dev) should be marked as dev dependencies
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedNestedPackageLock3WithDevOptionalDependencies(this.packageLockJsonFileName, componentName0, version0, componentName1, version1);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""devDependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }},
                ""peerDependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(2);

        // devOptional packages should be marked as dev dependencies
        var component0 = detectedComponents.First(x => x.Component.Id.Contains(componentName0));
        componentRecorder.GetEffectiveDevDependencyValue(component0.Component.Id).Should().BeTrue();

        var component1 = detectedComponents.First(x => x.Component.Id.Contains(componentName1));
        componentRecorder.GetEffectiveDevDependencyValue(component1.Component.Id).Should().BeTrue();

        foreach (var component in detectedComponents)
        {
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithPeerDependenciesReturnsValidAsync()
    {
        // Test that peer dependencies without dev flag should NOT be marked as dev dependencies
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedNestedPackageLock3WithPeerDependencies(this.packageLockJsonFileName, componentName0, version0, componentName1, version1);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }},
                ""peerDependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(2);

        // Peer-only packages (without dev flag) should NOT be marked as dev dependencies
        var component0 = detectedComponents.First(x => x.Component.Id.Contains(componentName0));
        componentRecorder.GetEffectiveDevDependencyValue(component0.Component.Id).Should().BeFalse();

        var component1 = detectedComponents.First(x => x.Component.Id.Contains(componentName1));
        componentRecorder.GetEffectiveDevDependencyValue(component1.Component.Id).Should().BeFalse();

        foreach (var component in detectedComponents)
        {
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithEnginesAsArray_DoesNotThrowAndReturnsSuccessAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""requires"": true,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""^{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                        ""engines"": [
                            ""node >= 18""
                        ]
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().ContainSingle(x => ((NpmComponent)x.Component).Name.Equals(componentName0));

        foreach (var component in detectedComponents)
        {
            ((NpmComponent)component.Component).Hash.Should().NotBeNullOrWhiteSpace();
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithLinkPackages_ShouldSkipLinkPackagesAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var linkPackageName = "linked-package";

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ==""
                    }},
                    ""node_modules/{2}"": {{
                        ""resolved"": ""../local-workspace/{2}"",
                        ""link"": true
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, linkPackageName);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(1);
        detectedComponents.Should().ContainSingle(x => ((NpmComponent)x.Component).Name.Equals(componentName0));

        // Link package should not be detected
        detectedComponents.Should().NotContain(x => ((NpmComponent)x.Component).Name.Equals(linkPackageName));
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithBundledDependencies_ShouldSkipBundledAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var bundledName = "bundled-dep";
        var bundledVersion = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ==""
                    }},
                    ""node_modules/{2}"": {{
                        ""version"": ""{3}"",
                        ""inBundle"": true,
                        ""resolved"": ""https://registry.npmjs.org/{2}/-/{2}-{3}.tgz"",
                        ""integrity"": ""sha512-ABC123XYZ==""
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, bundledName, bundledVersion);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(1);
        detectedComponents.Should().ContainSingle(x => ((NpmComponent)x.Component).Name.Equals(componentName0));

        // Bundled dependency should not be detected
        detectedComponents.Should().NotContain(x => ((NpmComponent)x.Component).Name.Equals(bundledName));
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithMissingVersions_ShouldSkipInvalidPackagesAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var invalidPackageName = "invalid-package";

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ==""
                    }},
                    ""node_modules/{2}"": {{
                        ""resolved"": ""https://registry.npmjs.org/{2}/-/{2}.tgz"",
                        ""integrity"": ""sha512-ABC123XYZ==""
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, invalidPackageName);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(1);
        detectedComponents.Should().ContainSingle(x => ((NpmComponent)x.Component).Name.Equals(componentName0));

        // Package without version should not be detected
        detectedComponents.Should().NotContain(x => ((NpmComponent)x.Component).Name.Equals(invalidPackageName));
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithComponentAtMultiplePaths_ShouldTrackDevStatusCorrectlyAsync()
    {
        // Test that a component appearing multiple times is only dev if ALL instances are dev
        var componentName = Guid.NewGuid().ToString("N");
        var version = NewRandomVersion();
        var depName = Guid.NewGuid().ToString("N");
        var depVersion = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }},
                        ""devDependencies"": {{
                            ""{2}"": ""{3}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                        ""dependencies"": {{
                            ""{4}"": ""{5}""
                        }}
                    }},
                    ""node_modules/{2}"": {{
                        ""version"": ""{3}"",
                        ""resolved"": ""https://registry.npmjs.org/{2}/-/{2}-{3}.tgz"",
                        ""integrity"": ""sha512-ABC123XYZ=="",
                        ""dev"": true,
                        ""dependencies"": {{
                            ""{4}"": ""{5}""
                        }}
                    }},
                    ""node_modules/{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://registry.npmjs.org/{4}/-/{4}-{5}.tgz"",
                        ""integrity"": ""sha512-XYZ789ABC==""
                    }}
                }}
            }}";

        var sharedDepName = Guid.NewGuid().ToString("N");
        var sharedDepVersion = NewRandomVersion();
        var packageLockTemplate = string.Format(packageLockJson, componentName, version, depName, depVersion, sharedDepName, sharedDepVersion);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }},
                ""devDependencies"": {{
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, componentName, version, depName, depVersion);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(3);

        // The shared dependency appears in both dev and non-dev contexts, so should NOT be marked as dev
        var sharedDep = detectedComponents.First(x => ((NpmComponent)x.Component).Name.Equals(sharedDepName));
        componentRecorder.GetEffectiveDevDependencyValue(sharedDep.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithUnresolvableDependency_ShouldHandleGracefullyAsync()
    {
        // Test that dependencies that cannot be resolved are logged but don't cause failure
        var componentName = Guid.NewGuid().ToString("N");
        var version = NewRandomVersion();
        var unresolvedDep = "unresolved-dep";

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                        ""dependencies"": {{
                            ""{2}"": ""1.0.0""
                        }}
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName, version, unresolvedDep);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, componentName, version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        // Should not fail even though dependency is missing
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(1);
        detectedComponents.Should().ContainSingle(x => ((NpmComponent)x.Component).Name.Equals(componentName));
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3WithScopedNestedDependencies_ShouldResolveCorrectlyAsync()
    {
        // Test nested node_modules resolution with scoped packages
        var scopedPkg = "@scope/package";
        var version = NewRandomVersion();
        var nestedPkg = "nested-pkg";
        var nestedVersion = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://registry.npmjs.org/{0}/-/{0}-{1}.tgz"",
                        ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                        ""dependencies"": {{
                            ""{2}"": ""{3}""
                        }}
                    }},
                    ""node_modules/{0}/node_modules/{2}"": {{
                        ""version"": ""{3}"",
                        ""resolved"": ""https://registry.npmjs.org/{2}/-/{2}-{3}.tgz"",
                        ""integrity"": ""sha512-ABC123XYZ==""
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, scopedPkg, version, nestedPkg, nestedVersion);

        var packageJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packageJson, scopedPkg, version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        detectedComponents.Should().HaveCount(2);

        // Verify the dependency edge exists
        var parentId = detectedComponents.First(x => ((NpmComponent)x.Component).Name.Equals(scopedPkg)).Component.Id;
        var childId = detectedComponents.First(x => ((NpmComponent)x.Component).Name.Equals(nestedPkg)).Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();
        dependencyGraph.GetDependenciesForComponent(parentId).Should().Contain(childId);
    }
}

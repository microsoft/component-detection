#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
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
}

namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetMSBuildBinaryLogComponentDetectorTests : BaseDetectorTest<NuGetMSBuildBinaryLogComponentDetector>
{
    [TestMethod]
    public async Task DependenciesAreReportedForEachProjectFile()
    {
        // the contents of `projectContents` are the root entrypoint to the detector, but MSBuild will crawl to the other project file
        var (scanResult, componentRecorder) = await this.ExecuteDetectorAndGetBinLogAsync(
            projectContents: $@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                  <PropertyGroup>
                    <TargetFramework>{MSBuildTestUtilities.TestTargetFramework}</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include=""other-project/other-project.csproj"" />
                  </ItemGroup>
                </Project>
                ",
            additionalFiles: new[]
                {
                    ("other-project/other-project.csproj", $@"
                        <Project Sdk=""Microsoft.NET.Sdk"">
                          <PropertyGroup>
                            <TargetFramework>{MSBuildTestUtilities.TestTargetFramework}</TargetFramework>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageReference Include=""Some.Package"" Version=""1.2.3"" />
                          </ItemGroup>
                        </Project>
                        "),
                },
            mockedPackages: new[]
                {
                    ("Some.Package", "1.2.3", MSBuildTestUtilities.TestTargetFramework, "<dependencies><dependency id=\"Transitive.Dependency\" version=\"4.5.6\" /></dependencies>"),
                    ("Transitive.Dependency", "4.5.6", MSBuildTestUtilities.TestTargetFramework, null),
                });

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // components are reported for each project file
        var originalFileComponents = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/project.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        originalFileComponents.Should().Equal("Some.Package/1.2.3", "Transitive.Dependency/4.5.6");

        var referencedFileComponents = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/other-project/other-project.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        referencedFileComponents.Should().Equal("Some.Package/1.2.3", "Transitive.Dependency/4.5.6");
    }

    [TestMethod]
    public async Task RemovedPackagesAreNotReported()
    {
        // This is a very specific scenario that should be tested, but we don't want any changes to this repo's SDK to
        // change the outcome of the test, so we're doing it manually.  The scenario is the SDK knowingly replacing an
        // assembly from an outdated transitive package.  One example common in the wild is the package
        // `Microsoft.Extensions.Configuration.Json/6.0.0` which contains a transitive dependency on
        // `System.Text.Json/6.0.0`, but the SDK version 6.0.424 or later pulls this reference out of the dependency
        // set and replaces the .dll with a local updated copy.  The end result is the `package.assets.json` file
        // reports that `System.Text.Json/6.0.0` is referenced by a project, but after build and at runtime, this isn't
        // the case and can lead to false positives when reporting dependencies.  The way the SDK accomplishes this is
        // by removing `System.Text.Json.dll` from the group `@(RuntimeCopyLocalItems)`.  To accomplish this in the
        // test, we're inserting a custom target that does this same action.
        var (scanResult, componentRecorder) = await this.ExecuteDetectorAndGetBinLogAsync(
            projectContents: $@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                  <PropertyGroup>
                    <TargetFramework>{MSBuildTestUtilities.TestTargetFramework}</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include=""Some.Package"" Version=""1.2.3"" />
                  </ItemGroup>
                  <Target Name=""TEST_RemovePackageWithOutdatedVersion"" BeforeTargets=""GenerateBuildDependencyFile"" AfterTargets=""ResolveAssemblyReferences"">
                    <ItemGroup>
                      <!-- The SDK removes the specific .dll with metadata from the original package. -->
                      <RuntimeCopyLocalItems Remove=""$(NUGET_PACKAGES)/**/Transitive.Dependency.dll"" NuGetPackageId=""Transitive.Dependency"" NuGetPackageVersion=""4.5.6"" />
                    </ItemGroup>
                  </Target>
                </Project>
                ",
            mockedPackages: new[]
                {
                    ("Some.Package", "1.2.3", MSBuildTestUtilities.TestTargetFramework, "<dependencies><dependency id=\"Transitive.Dependency\" version=\"4.5.6\" /></dependencies>"),
                    ("Transitive.Dependency", "4.5.6", MSBuildTestUtilities.TestTargetFramework, null),
                });

        var detectedComponents = componentRecorder.GetDetectedComponents();

        var packages = detectedComponents.Select(d => d.Component).Cast<NuGetComponent>().OrderBy(c => c.Name).Select(c => $"{c.Name}/{c.Version}");
        packages.Should().Equal("Some.Package/1.2.3");
    }

    private async Task<(Contracts.IndividualDetectorScanResult ScanResult, Contracts.IComponentRecorder ComponentRecorder)> ExecuteDetectorAndGetBinLogAsync(
        string projectContents,
        (string FileName, string Content)[] additionalFiles = null,
        (string Name, string Version, string TargetFramework, string DependenciesXml)[] mockedPackages = null)
    {
        using var binLogStream = await MSBuildTestUtilities.GetBinLogStreamFromFileContentsAsync(projectContents, additionalFiles, mockedPackages);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("msbuild.binlog", binLogStream)
            .ExecuteDetectorAsync();
        return (scanResult, componentRecorder);
    }
}

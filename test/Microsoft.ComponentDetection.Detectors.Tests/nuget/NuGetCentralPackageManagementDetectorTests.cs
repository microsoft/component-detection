namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet;

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class NuGetCentralPackageManagementDetectorTests : BaseDetectorTest<NuGetCentralPackageManagementDetector>
{
    [TestMethod]
    public async Task Should_DetectPackagesInDirectoryPackagesPropsAsync()
    {
        var directoryPackagesProps =
            @"<Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include=""Newtonsoft.Json"" Version=""13.0.3"" />
                    <PackageVersion Include=""Microsoft.Extensions.Logging"" Version=""7.0.0"" />
                    <PackageVersion Include=""System.Text.Json"" Version=""7.0.3"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Directory.Packages.props", directoryPackagesProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var newtonsoftComponent = new DetectedComponent(new NuGetComponent("Newtonsoft.Json", "13.0.3"));
        var loggingComponent = new DetectedComponent(new NuGetComponent("Microsoft.Extensions.Logging", "7.0.0"));
        var textJsonComponent = new DetectedComponent(new NuGetComponent("System.Text.Json", "7.0.3"));

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(3)
            .And.ContainEquivalentOf(newtonsoftComponent)
            .And.ContainEquivalentOf(loggingComponent)
            .And.ContainEquivalentOf(textJsonComponent);

        // Verify all components are marked as explicitly referenced
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();
        dependencyGraph.IsComponentExplicitlyReferenced(newtonsoftComponent.Component.Id).Should().BeTrue();
        dependencyGraph.IsComponentExplicitlyReferenced(loggingComponent.Component.Id).Should().BeTrue();
        dependencyGraph.IsComponentExplicitlyReferenced(textJsonComponent.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task Should_DetectGlobalPackageReferencesAsync()
    {
        var directoryPackagesProps =
            @"<Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include=""Newtonsoft.Json"" Version=""13.0.3"" />
                    <GlobalPackageReference Include=""Nerdbank.GitVersioning"" Version=""3.5.109"" />
                    <GlobalPackageReference Include=""Microsoft.CodeAnalysis.Analyzers"" Version=""3.3.4"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Directory.Packages.props", directoryPackagesProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var newtonsoftComponent = new DetectedComponent(new NuGetComponent("Newtonsoft.Json", "13.0.3"));
        var gitVersioningComponent = new DetectedComponent(new NuGetComponent("Nerdbank.GitVersioning", "3.5.109"));
        var analyzersComponent = new DetectedComponent(new NuGetComponent("Microsoft.CodeAnalysis.Analyzers", "3.3.4"));

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(3)
            .And.ContainEquivalentOf(newtonsoftComponent)
            .And.ContainEquivalentOf(gitVersioningComponent)
            .And.ContainEquivalentOf(analyzersComponent);

        // Verify all components are marked as explicitly referenced
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();
        dependencyGraph.IsComponentExplicitlyReferenced(newtonsoftComponent.Component.Id).Should().BeTrue();
        dependencyGraph.IsComponentExplicitlyReferenced(gitVersioningComponent.Component.Id).Should().BeTrue();
        dependencyGraph.IsComponentExplicitlyReferenced(analyzersComponent.Component.Id).Should().BeTrue();

        // Verify global package references are marked as development dependencies
        dependencyGraph.IsDevelopmentDependency(gitVersioningComponent.Component.Id).Should().BeTrue();
        dependencyGraph.IsDevelopmentDependency(analyzersComponent.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task Should_DetectPackagesInPackagesPropsFileAsync()
    {
        var packagesProps =
            @"<Project>
                <ItemGroup>
                    <PackageVersion Include=""AutoMapper"" Version=""12.0.1"" />
                    <PackageVersion Include=""FluentAssertions"" Version=""6.11.0"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("packages.props", packagesProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var autoMapperComponent = new DetectedComponent(new NuGetComponent("AutoMapper", "12.0.1"));
        var fluentAssertionsComponent = new DetectedComponent(new NuGetComponent("FluentAssertions", "6.11.0"));

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(2)
            .And.ContainEquivalentOf(autoMapperComponent)
            .And.ContainEquivalentOf(fluentAssertionsComponent);
    }

    [TestMethod]
    public async Task Should_DetectPackagesInPackagePropsFileAsync()
    {
        var packageProps =
            @"<Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include=""Serilog"" Version=""3.0.1"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("package.props", packageProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var serilogComponent = new DetectedComponent(new NuGetComponent("Serilog", "3.0.1"));

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(1)
            .And.ContainEquivalentOf(serilogComponent);
    }

    [TestMethod]
    public async Task Should_SkipNonCentralPackageManagementFileAsync()
    {
        var regularProps =
            @"<Project>
                <PropertyGroup>
                    <TargetFramework>net6.0</TargetFramework>
                    <SomeOtherProperty>value</SomeOtherProperty>
                </PropertyGroup>
                <ItemGroup>
                    <ProjectReference Include=""../Other.Project/Other.Project.csproj"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Directory.Build.props", regularProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Should_SkipPackageVersionElementsWithMissingAttributesAsync()
    {
        var directoryPackagesProps =
            @"<Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include=""ValidPackage"" Version=""1.0.0"" />
                    <PackageVersion Include=""MissingVersion"" />
                    <PackageVersion Version=""2.0.0"" />
                    <PackageVersion Include="""" Version=""3.0.0"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Directory.Packages.props", directoryPackagesProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var validComponent = new DetectedComponent(new NuGetComponent("ValidPackage", "1.0.0"));

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(1)
            .And.ContainEquivalentOf(validComponent);
    }

    [TestMethod]
    public async Task Should_HandleMalformedXmlGracefullyAsync()
    {
        var malformedProps =
            @"<Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include=""Package"" Version=""1.0.0""
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Directory.Packages.props", malformedProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Should_DetectPackagesWithConditionalVersionsAsync()
    {
        var directoryPackagesProps =
            @"<Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include=""ConditionalPackage"" Version=""1.0.0"" Condition=""'$(TargetFramework)' == 'netstandard2.0'"" />
                    <PackageVersion Include=""ConditionalPackage"" Version=""2.0.0"" Condition=""'$(TargetFramework)' == 'net6.0'"" />
                    <PackageVersion Include=""RegularPackage"" Version=""3.0.0"" />
                </ItemGroup>
              </Project>";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Directory.Packages.props", directoryPackagesProps)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var conditionalComponent1 = new DetectedComponent(new NuGetComponent("ConditionalPackage", "1.0.0"));
        var conditionalComponent2 = new DetectedComponent(new NuGetComponent("ConditionalPackage", "2.0.0"));
        var regularComponent = new DetectedComponent(new NuGetComponent("RegularPackage", "3.0.0"));

        detectedComponents.Should().NotBeEmpty()
            .And.HaveCount(3)
            .And.ContainEquivalentOf(conditionalComponent1)
            .And.ContainEquivalentOf(conditionalComponent2)
            .And.ContainEquivalentOf(regularComponent);
    }
}

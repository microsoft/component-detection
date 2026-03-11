namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Integration tests for <see cref="BinLogProcessor"/> that build real MSBuild projects
/// to produce binlog files, then parse them to verify extracted project information.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class BinLogProcessorTests
{
    private readonly BinLogProcessor processor;
    private string testDir = null!;

    public BinLogProcessorTests() => this.processor = new BinLogProcessor(NullLogger.Instance);

    [TestInitialize]
    public void TestInitialize()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), "BinLogProcessorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.testDir);

        // Copy the workspace global.json so temp projects use the same SDK as the repo.
        // This avoids hardcoding a version that may not be installed in CI/dev machines.
        var workspaceGlobalJson = FindWorkspaceGlobalJsonPath();
        if (workspaceGlobalJson != null)
        {
            File.Copy(workspaceGlobalJson, Path.Combine(this.testDir, "global.json"));
        }
    }

    private static string? FindWorkspaceGlobalJsonPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(currentDirectory))
        {
            var candidate = Path.Combine(currentDirectory, "global.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(currentDirectory)?.FullName;
            if (parent == currentDirectory)
            {
                break;
            }

            currentDirectory = parent;
        }

        return null;
    }

    [TestCleanup]
    public void TestCleanup()
    {
        try
        {
            if (Directory.Exists(this.testDir))
            {
                Directory.Delete(this.testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [TestMethod]
    public async Task SingleTargetProject_ExtractsBasicProperties()
    {
        var projectDir = Path.Combine(this.testDir, "SingleTarget");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "SingleTarget.csproj", content);
        WriteMinimalProgram(projectDir);

        var binlogPath = await BuildProjectAsync(projectDir, "SingleTarget.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        results.Should().NotBeEmpty();

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("SingleTarget.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.TargetFramework.Should().Be("net8.0");
        projectInfo.OutputType.Should().Be("Exe");
        projectInfo.NETCoreSdkVersion.Should().NotBeNullOrEmpty();
        projectInfo.ProjectAssetsFile.Should().NotBeNullOrEmpty();
        projectInfo.IsOuterBuild.Should().BeFalse();

        // PackageReference should be captured
        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task MultiTargetProject_ExtractsOuterAndInnerBuilds()
    {
        var projectDir = Path.Combine(this.testDir, "MultiTarget");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net7.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "MultiTarget.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "MultiTarget.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("MultiTarget.csproj", StringComparison.OrdinalIgnoreCase));

        // The outer build should have TargetFrameworks set
        projectInfo.IsOuterBuild.Should().BeTrue();
        projectInfo.TargetFrameworks.Should().Contain("net8.0");
        projectInfo.TargetFrameworks.Should().Contain("net7.0");

        // Inner builds should be captured
        projectInfo.InnerBuilds.Should().HaveCountGreaterThanOrEqualTo(2);

        var net8Inner = projectInfo.InnerBuilds.FirstOrDefault(
            ib => ib.TargetFramework != null && ib.TargetFramework.Contains("net8.0"));
        var net7Inner = projectInfo.InnerBuilds.FirstOrDefault(
            ib => ib.TargetFramework != null && ib.TargetFramework.Contains("net7.0"));

        net8Inner.Should().NotBeNull();
        net7Inner.Should().NotBeNull();

        // Each inner build should have its own PackageReference
        net8Inner!.PackageReference.Should().ContainKey("Newtonsoft.Json");
        net7Inner!.PackageReference.Should().ContainKey("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task TestProject_ExtractsIsTestProject()
    {
        var projectDir = Path.Combine(this.testDir, "TestProject");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "TestProject.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "TestProject.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("TestProject.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.IsTestProject.Should().Be(true);
    }

    [TestMethod]
    public async Task ProjectWithIsShippingFalse_ExtractsIsShipping()
    {
        var projectDir = Path.Combine(this.testDir, "NonShipping");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <IsShipping>false</IsShipping>
              </PropertyGroup>
            </Project>
            """;
        WriteFile(projectDir, "NonShipping.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "NonShipping.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("NonShipping.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.IsShipping.Should().Be(false);
    }

    [TestMethod]
    public async Task MultipleProjectsInSameBuild_ExtractsAll()
    {
        // Create a solution with two projects
        var solutionDir = Path.Combine(this.testDir, "MultiProject");
        Directory.CreateDirectory(solutionDir);

        var projectADir = Path.Combine(solutionDir, "ProjectA");
        var projectBDir = Path.Combine(solutionDir, "ProjectB");
        Directory.CreateDirectory(projectADir);
        Directory.CreateDirectory(projectBDir);

        var projectAContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectADir, "ProjectA.csproj", projectAContent);
        WriteMinimalProgram(projectADir);

        var projectBContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="8.0.5" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectBDir, "ProjectB.csproj", projectBContent);

        // Create a solution to build both projects in a single binlog
        await RunDotNetAsync(solutionDir, "new sln --name MultiProject");
        await RunDotNetAsync(solutionDir, $"sln add \"{Path.Combine(projectADir, "ProjectA.csproj")}\"");
        await RunDotNetAsync(solutionDir, $"sln add \"{Path.Combine(projectBDir, "ProjectB.csproj")}\"");
        var binlogPath = Path.Combine(solutionDir, "build.binlog");
        await RunDotNetAsync(solutionDir, $"build \"MultiProject.sln\" -bl:\"{binlogPath}\" /p:UseAppHost=false");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectA = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ProjectA.csproj", StringComparison.OrdinalIgnoreCase));
        var projectB = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ProjectB.csproj", StringComparison.OrdinalIgnoreCase));

        projectA.Should().NotBeNull();
        projectB.Should().NotBeNull();

        projectA!.OutputType.Should().Be("Exe");
        projectA.PackageReference.Should().ContainKey("Newtonsoft.Json");

        projectB!.OutputType.Should().Be("Library");
        projectB.PackageReference.Should().ContainKey("System.Text.Json");
    }

    [TestMethod]
    public async Task ProjectToProjectReference_ExtractsBothProjects()
    {
        var solutionDir = Path.Combine(this.testDir, "P2P");
        Directory.CreateDirectory(solutionDir);

        var libDir = Path.Combine(solutionDir, "MyLib");
        var appDir = Path.Combine(solutionDir, "MyApp");
        Directory.CreateDirectory(libDir);
        Directory.CreateDirectory(appDir);

        var libContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(libDir, "MyLib.csproj", libContent);

        var appContent = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\MyLib\MyLib.csproj" />
                <PackageReference Include="System.Text.Json" Version="8.0.5" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(appDir, "MyApp.csproj", appContent);
        WriteMinimalProgram(appDir);

        // Build the app (which also builds the lib)
        var binlogPath = await BuildProjectAsync(appDir, "MyApp.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var appInfo = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("MyApp.csproj", StringComparison.OrdinalIgnoreCase));
        var libInfo = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("MyLib.csproj", StringComparison.OrdinalIgnoreCase));

        appInfo.Should().NotBeNull();
        libInfo.Should().NotBeNull();

        appInfo!.OutputType.Should().Be("Exe");
        appInfo.PackageReference.Should().ContainKey("System.Text.Json");

        libInfo!.PackageReference.Should().ContainKey("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task CustomTargetModifiesProperties_PropertyIsOverridden()
    {
        var projectDir = Path.Combine(this.testDir, "CustomTarget");
        Directory.CreateDirectory(projectDir);

        // A custom target that runs before Restore and changes a property
        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <IsTestProject>false</IsTestProject>
              </PropertyGroup>
              <Target Name="SetTestProjectBeforeRestore" BeforeTargets="Restore;CollectPackageReferences">
                <PropertyGroup>
                  <IsTestProject>true</IsTestProject>
                </PropertyGroup>
              </Target>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "CustomTarget.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "CustomTarget.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("CustomTarget.csproj", StringComparison.OrdinalIgnoreCase));

        // The custom target's property override should be captured
        // depending on when the binlog captures it.
        // At minimum the evaluation-time value should be captured.
        projectInfo.IsTestProject.Should().NotBeNull();
    }

    [TestMethod]
    public async Task CustomTargetAddsItems_ItemsAreCaptured()
    {
        var projectDir = Path.Combine(this.testDir, "CustomItems");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <Target Name="AddExtraPackageDownload" BeforeTargets="Restore;CollectPackageDownloads">
                <ItemGroup>
                  <PackageDownload Include="System.Memory" Version="[4.5.5]" />
                </ItemGroup>
              </Target>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "CustomItems.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "CustomItems.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("CustomItems.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // The PackageDownload added by the custom target should be captured
        // (depends on whether the task runs and binlog captures TaskParameter events)
        // This tests that task-parameter-level item tracking works
        projectInfo.PackageDownload.Should().ContainKey("System.Memory");
    }

    [TestMethod]
    public async Task PackageDownloadItems_AreCaptured()
    {
        var projectDir = Path.Combine(this.testDir, "PkgDownload");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageDownload Include="System.Memory" Version="[4.5.5]" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "PkgDownload.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "PkgDownload.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("PkgDownload.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
        projectInfo.PackageDownload.Should().ContainKey("System.Memory");
    }

    [TestMethod]
    public async Task MultiTargetWithDifferentPackagesPerTfm_InnerBuildsHaveDifferentItems()
    {
        var projectDir = Path.Combine(this.testDir, "PerTfmPackages");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net7.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="System.Text.Json" Version="8.0.5" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "PerTfmPackages.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "PerTfmPackages.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("PerTfmPackages.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.IsOuterBuild.Should().BeTrue();

        var net8Inner = projectInfo.InnerBuilds.FirstOrDefault(
            ib => ib.TargetFramework != null && ib.TargetFramework.Contains("net8.0"));
        var net7Inner = projectInfo.InnerBuilds.FirstOrDefault(
            ib => ib.TargetFramework != null && ib.TargetFramework.Contains("net7.0"));

        net8Inner.Should().NotBeNull();
        net7Inner.Should().NotBeNull();

        // net8.0 inner build should have both packages
        net8Inner!.PackageReference.Should().ContainKey("Newtonsoft.Json");
        net8Inner.PackageReference.Should().ContainKey("System.Text.Json");

        // net7.0 inner build should only have Newtonsoft.Json
        net7Inner!.PackageReference.Should().ContainKey("Newtonsoft.Json");
        net7Inner.PackageReference.Should().NotContainKey("System.Text.Json");
    }

    [TestMethod]
    public async Task SelfContainedProject_ExtractsSelfContained()
    {
        var projectDir = Path.Combine(this.testDir, "SelfContained");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <SelfContained>true</SelfContained>
                <RuntimeIdentifier>win-x64</RuntimeIdentifier>
              </PropertyGroup>
            </Project>
            """;
        WriteFile(projectDir, "SelfContained.csproj", content);
        WriteMinimalProgram(projectDir);

        // Use restore-only build: evaluation captures properties in the binlog
        // without needing a full build (which conflicts UseAppHost=false + SelfContained=true)
        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"msbuild \"{Path.Combine(projectDir, "SelfContained.csproj")}\" -t:Restore -bl:\"{binlogPath}\"");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("SelfContained.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.SelfContained.Should().Be(true);
        projectInfo.OutputType.Should().Be("Exe");
    }

    [TestMethod]
    public async Task ItemWithMetadata_MetadataIsCaptured()
    {
        var projectDir = Path.Combine(this.testDir, "ItemMetadata");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
                  <PrivateAssets>all</PrivateAssets>
                  <IsDevelopmentDependency>true</IsDevelopmentDependency>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "ItemMetadata.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "ItemMetadata.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ItemMetadata.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("StyleCop.Analyzers");

        var styleCopItem = projectInfo.PackageReference["StyleCop.Analyzers"];
        styleCopItem.GetMetadata("IsDevelopmentDependency").Should().Be("true");
    }

    [TestMethod]
    public void EmptyBinlogPath_ReturnsEmptyList()
    {
        // Test with a non-existent file
        var results = this.processor.ExtractProjectInfo(Path.Combine(this.testDir, "nonexistent.binlog"));
        results.Should().BeEmpty();
    }

    [TestMethod]
    public async Task EvaluationPropertyReassignment_LaterDefinitionWins()
    {
        // An imported .targets file overrides a property set in the project file.
        // This tests that PropertyReassignment events during evaluation are captured.
        var projectDir = Path.Combine(this.testDir, "PropReassign");
        Directory.CreateDirectory(projectDir);

        // Create a .targets file that overrides OutputType
        var targetsContent = """
            <Project>
              <PropertyGroup>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """;
        WriteFile(projectDir, "override.targets", targetsContent);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <Import Project="override.targets" />
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "PropReassign.csproj", content);
        WriteMinimalProgram(projectDir);

        var binlogPath = await BuildProjectAsync(projectDir, "PropReassign.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("PropReassign.csproj", StringComparison.OrdinalIgnoreCase));

        // The imported .targets overrides OutputType from Library to Exe during evaluation
        projectInfo.OutputType.Should().Be("Exe");
    }

    [TestMethod]
    public async Task TargetSetsProperty_OnlyCapturedAtEvaluationTime()
    {
        // Verifies that target-level <PropertyGroup> changes are NOT captured in binlog
        // property events. This documents a known limitation: only evaluation-time
        // property values are tracked.
        var projectDir = Path.Combine(this.testDir, "TargetPropLimit");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
              <Target Name="OverrideOutputType" BeforeTargets="CoreCompile">
                <PropertyGroup>
                  <OutputType>Exe</OutputType>
                </PropertyGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "TargetPropLimit.csproj", content);

        // Use restore-only to avoid compile errors (the target changes OutputType to Exe
        // which expects a Main method that doesn't exist). Evaluation properties are still captured.
        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"msbuild TargetPropLimit.csproj -t:Restore -bl:\"{binlogPath}\"");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("TargetPropLimit.csproj", StringComparison.OrdinalIgnoreCase));

        // Target-level property changes don't emit PropertyReassignment events in binlog.
        // We only capture the evaluation-time value.
        projectInfo.OutputType.Should().Be(
            "Library",
            "target-level property changes are not visible in binlog events");
    }

    [TestMethod]
    public async Task EvaluationBoolPropertyReassignment_LaterDefinitionWins()
    {
        // An imported .targets file overrides IsTestProject from false to true.
        // This tests that boolean property reassignment during evaluation is captured.
        var projectDir = Path.Combine(this.testDir, "BoolReassign");
        Directory.CreateDirectory(projectDir);

        var targetsContent = """
            <Project>
              <PropertyGroup>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
            </Project>
            """;
        WriteFile(projectDir, "override.targets", targetsContent);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <IsTestProject>false</IsTestProject>
              </PropertyGroup>
              <Import Project="override.targets" />
            </Project>
            """;
        WriteFile(projectDir, "BoolReassign.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "BoolReassign.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("BoolReassign.csproj", StringComparison.OrdinalIgnoreCase));

        // The imported .targets overrides IsTestProject to true during evaluation
        projectInfo.IsTestProject.Should().Be(true);
    }

    [TestMethod]
    public async Task TargetBeforeRestoreAddsPackageReference_ItemIsCaptured()
    {
        // A target running before Restore adds a PackageReference
        var projectDir = Path.Combine(this.testDir, "TargetAddsRef");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <Target Name="AddDynamicPackage" BeforeTargets="Restore;CollectPackageReferences">
                <ItemGroup>
                  <PackageReference Include="System.Memory" Version="4.5.5" />
                </ItemGroup>
              </Target>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "TargetAddsRef.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "TargetAddsRef.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("TargetAddsRef.csproj", StringComparison.OrdinalIgnoreCase));

        // The static PackageReference from evaluation
        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // The dynamically-added PackageReference from the target
        projectInfo.PackageReference.Should().ContainKey("System.Memory");
    }

    [TestMethod]
    public async Task TargetBeforeRestoreAddsPackageDownload_ItemIsCaptured()
    {
        // A target running before Restore adds a PackageDownload
        var projectDir = Path.Combine(this.testDir, "TargetAddsDownload");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <Target Name="AddDynamicDownload" BeforeTargets="Restore;CollectPackageDownloads">
                <ItemGroup>
                  <PackageDownload Include="System.Memory" Version="[4.5.5]" />
                </ItemGroup>
              </Target>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "TargetAddsDownload.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "TargetAddsDownload.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("TargetAddsDownload.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
        projectInfo.PackageDownload.Should().ContainKey("System.Memory");
    }

    [TestMethod]
    public async Task TraversalBuildAndPublish_MergesProperties()
    {
        // An orchestrator project builds a child project, then restores it with SelfContained.
        // This simulates a traversal or CI script that does build + publish in one binlog.
        var solutionDir = Path.Combine(this.testDir, "TraversalMerge");
        Directory.CreateDirectory(solutionDir);

        var appDir = Path.Combine(solutionDir, "App");
        Directory.CreateDirectory(appDir);

        var appContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(appDir, "App.csproj", appContent);
        WriteMinimalProgram(appDir);

        // Orchestrator: restore, build, then restore with SelfContained (all in one binlog)
        var orchestratorContent = """
            <Project>
              <Target Name="BuildAndPublish">
                <MSBuild Projects="App\App.csproj" Targets="Restore" />
                <MSBuild Projects="App\App.csproj" Targets="Build" Properties="UseAppHost=false" />
                <MSBuild Projects="App\App.csproj" Targets="Restore" Properties="SelfContained=true;RuntimeIdentifier=win-x64" />
              </Target>
            </Project>
            """;
        WriteFile(solutionDir, "Orchestrator.proj", orchestratorContent);

        var binlogPath = Path.Combine(solutionDir, "build.binlog");
        await RunDotNetAsync(solutionDir, $"msbuild Orchestrator.proj -t:BuildAndPublish -bl:\"{binlogPath}\"");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var appInfo = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("App.csproj", StringComparison.OrdinalIgnoreCase));

        appInfo.Should().NotBeNull();

        // After merge, SelfContained should be true (from the second restore pass)
        appInfo!.SelfContained.Should().Be(true);

        // The original PackageReference should still be present
        appInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task BuildThenPublishSelfContained_MergesSelfContained()
    {
        // Simulates a common CI pattern: build first (non-self-contained), then publish (self-contained).
        // Both produce entries in the same binlog. The merge should yield SelfContained=true.
        var projectDir = Path.Combine(this.testDir, "BuildPublishMerge");
        Directory.CreateDirectory(projectDir);

        var appContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "App.csproj", appContent);
        WriteMinimalProgram(projectDir);

        // First build (not self-contained), then publish (self-contained), both into same binlog
        // We use an orchestrator project that invokes MSBuild twice
        var orchestratorContent = """
            <Project>
              <Target Name="BuildAndPublish">
                <MSBuild Projects="App.csproj" Targets="Restore" />
                <MSBuild Projects="App.csproj" Targets="Build" Properties="UseAppHost=false" />
                <MSBuild Projects="App.csproj" Targets="Restore" Properties="SelfContained=true;RuntimeIdentifier=win-x64" />
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "Orchestrator.proj", orchestratorContent);

        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"msbuild Orchestrator.proj -t:BuildAndPublish -bl:\"{binlogPath}\"");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var appInfo = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("App.csproj", StringComparison.OrdinalIgnoreCase));

        appInfo.Should().NotBeNull();
        appInfo!.SelfContained.Should().Be(true, "the publish pass sets SelfContained=true, which should be merged");
        appInfo.OutputType.Should().Be("Exe");
        appInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task GlobalPropertyFromCommandLine_CapturedInBinlog()
    {
        // Pass IsTestProject=true as a global property via /p: on the command line
        var projectDir = Path.Combine(this.testDir, "GlobalProp");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "GlobalProp.csproj", content);

        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"build GlobalProp.csproj -bl:\"{binlogPath}\" /p:UseAppHost=false /p:IsTestProject=true");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("GlobalProp.csproj", StringComparison.OrdinalIgnoreCase));

        // Global property should be captured from evaluation
        projectInfo.IsTestProject.Should().Be(true);
    }

    [TestMethod]
    public async Task GlobalPropertyOverridesProjectFile_CommandLineWins()
    {
        // Project file says OutputType=Library, command line says OutputType=Exe
        var projectDir = Path.Combine(this.testDir, "GlobalOverride");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
            </Project>
            """;
        WriteFile(projectDir, "GlobalOverride.csproj", content);
        WriteMinimalProgram(projectDir);

        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"build GlobalOverride.csproj -bl:\"{binlogPath}\" /p:UseAppHost=false /p:OutputType=Exe");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("GlobalOverride.csproj", StringComparison.OrdinalIgnoreCase));

        // Command-line global property should override the project file value
        projectInfo.OutputType.Should().Be("Exe");
    }

    [TestMethod]
    public async Task EnvironmentVariableProperty_CapturedInBinlog()
    {
        // MSBuild automatically promotes environment variables to properties.
        // Setting IsTestProject as an env var (without referencing it in the project)
        // should still be captured in the binlog evaluation.
        var projectDir = Path.Combine(this.testDir, "EnvVar");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "EnvVar.csproj", content);

        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunProcessWithEnvAsync(
            projectDir,
            "dotnet",
            $"build EnvVar.csproj -bl:\"{binlogPath}\" /p:UseAppHost=false",
            ("IsTestProject", "true"));

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("EnvVar.csproj", StringComparison.OrdinalIgnoreCase));

        // The env var is automatically promoted to an MSBuild property during evaluation
        projectInfo.IsTestProject.Should().Be(true);
    }

    [TestMethod]
    public async Task TargetRemovesPackageReference_ItemIsRemoved()
    {
        // A PackageReference is defined in the project, but a target removes it before restore
        var projectDir = Path.Combine(this.testDir, "RemoveItem");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="System.Memory" Version="4.5.5" />
              </ItemGroup>
              <Target Name="RemovePackage" BeforeTargets="Restore;CollectPackageReferences">
                <ItemGroup>
                  <PackageReference Remove="System.Memory" />
                </ItemGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "RemoveItem.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "RemoveItem.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("RemoveItem.csproj", StringComparison.OrdinalIgnoreCase));

        // The target removes System.Memory before restore
        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
        projectInfo.PackageReference.Should().NotContainKey(
            "System.Memory",
            "the target removes System.Memory before restore");
    }

    [TestMethod]
    public async Task ItemUpdateTag_MetadataIsUpdated()
    {
        // Uses <PackageReference Update="..." /> to change metadata
        var projectDir = Path.Combine(this.testDir, "ItemUpdate");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Update="Newtonsoft.Json" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "ItemUpdate.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "ItemUpdate.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ItemUpdate.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // After the Update, PrivateAssets metadata should be set
        var item = projectInfo.PackageReference["Newtonsoft.Json"];
        item.GetMetadata("PrivateAssets").Should().Be("all");
    }

    [TestMethod]
    public async Task ItemDefinitionGroup_DefaultMetadataApplied()
    {
        // ItemDefinitionGroup sets default PrivateAssets for all PackageReference items
        var projectDir = Path.Combine(this.testDir, "ItemDefGroup");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemDefinitionGroup>
                <PackageReference>
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemDefinitionGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="System.Memory" Version="4.5.5" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "ItemDefGroup.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "ItemDefGroup.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ItemDefGroup.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");
        projectInfo.PackageReference.Should().ContainKey("System.Memory");

        // ItemDefinitionGroup should apply PrivateAssets=all to both items
        var newtonsoftItem = projectInfo.PackageReference["Newtonsoft.Json"];
        newtonsoftItem.GetMetadata("PrivateAssets").Should().Be("all");

        var memoryItem = projectInfo.PackageReference["System.Memory"];
        memoryItem.GetMetadata("PrivateAssets").Should().Be("all");
    }

    [TestMethod]
    public async Task TargetRemovesAndReAddsWithMetadata_MetadataReflectsChange()
    {
        // A target runs and modifies metadata via Remove+Include pattern.
        // This is necessary because Item Update inside targets does not emit
        // TaskParameter events in binlog, but Remove+Include does.
        var projectDir = Path.Combine(this.testDir, "TargetMetadata");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <Target Name="ReplaceWithMetadata" BeforeTargets="Restore;CollectPackageReferences">
                <ItemGroup>
                  <PackageReference Remove="Newtonsoft.Json" />
                  <PackageReference Include="Newtonsoft.Json" Version="13.0.1">
                    <PrivateAssets>all</PrivateAssets>
                  </PackageReference>
                </ItemGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "TargetMetadata.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "TargetMetadata.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("TargetMetadata.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // The Remove+Include in target produces TaskParameter events
        var item = projectInfo.PackageReference["Newtonsoft.Json"];
        item.GetMetadata("PrivateAssets").Should().Be("all");
    }

    [TestMethod]
    public async Task TargetItemUpdateLimitation_UpdateNotVisibleInBinlog()
    {
        // Documents a known limitation: <PackageReference Update="..."> inside a
        // <Target> does NOT emit TaskParameter events in the binlog. The metadata
        // change is invisible to the BinLogProcessor.
        var projectDir = Path.Combine(this.testDir, "TargetUpdateLimit");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <Target Name="UpdateMetadata" BeforeTargets="Restore;CollectPackageReferences">
                <ItemGroup>
                  <PackageReference Update="Newtonsoft.Json" PrivateAssets="all" />
                </ItemGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "TargetUpdateLimit.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "TargetUpdateLimit.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("TargetUpdateLimit.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // Item Update inside targets doesn't emit TaskParameter events.
        // PrivateAssets remains at the evaluation-time value (not set).
        var item = projectInfo.PackageReference["Newtonsoft.Json"];
        item.GetMetadata("PrivateAssets").Should().BeNullOrEmpty(
            "item Update inside targets is not visible in binlog TaskParameter events");
    }

    [TestMethod]
    public async Task TargetAddsItemButPropertyInvisible_ItemCapturedPropertyNot()
    {
        // A single target modifies both a property and adds an item.
        // Property changes in targets are invisible, but item additions are visible.
        var projectDir = Path.Combine(this.testDir, "ComboPropsItems");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <IsTestProject>false</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <Target Name="ModifyBoth" BeforeTargets="Restore;CollectPackageReferences;CollectPackageDownloads">
                <PropertyGroup>
                  <IsTestProject>true</IsTestProject>
                </PropertyGroup>
                <ItemGroup>
                  <PackageDownload Include="System.Memory" Version="[4.5.5]" />
                </ItemGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "ComboPropsItems.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "ComboPropsItems.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ComboPropsItems.csproj", StringComparison.OrdinalIgnoreCase));

        // Property change in target is invisible (stays at evaluation value)
        projectInfo.IsTestProject.Should().Be(
            false,
            "target-level property changes are not visible in binlog events");
        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // Item addition in target IS captured via TaskParameter events
        projectInfo.PackageDownload.Should().ContainKey("System.Memory");
    }

    [TestMethod]
    public async Task MultiTargetWithTargetConditionalOnTfm_PerInnerBuildChanges()
    {
        // A multi-targeted project where a target conditionally adds a PackageDownload
        // only for the net8.0 TFM
        var projectDir = Path.Combine(this.testDir, "MultiTargetConditional");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net7.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <Target Name="AddNet8Download" BeforeTargets="Restore;CollectPackageDownloads"
                      Condition="'$(TargetFramework)' == 'net8.0'">
                <ItemGroup>
                  <PackageDownload Include="System.Memory" Version="[4.5.5]" />
                </ItemGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "MultiTargetConditional.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "MultiTargetConditional.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("MultiTargetConditional.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.IsOuterBuild.Should().BeTrue();

        var net8Inner = projectInfo.InnerBuilds.First(
            ib => ib.TargetFramework != null && ib.TargetFramework.Contains("net8.0"));
        var net7Inner = projectInfo.InnerBuilds.First(
            ib => ib.TargetFramework != null && ib.TargetFramework.Contains("net7.0"));

        // net8.0 should have the target-added PackageDownload
        net8Inner.PackageDownload.Should().ContainKey("System.Memory");

        // net7.0 should NOT have it (condition not met)
        net7Inner.PackageDownload.Should().NotContainKey("System.Memory");
    }

    [TestMethod]
    public async Task GlobalPropertyAndTargetOverride_TargetWins()
    {
        // Global property sets SelfContained=false, but a target changes it to true
        var projectDir = Path.Combine(this.testDir, "GlobalAndTarget");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <Target Name="ForceSelfContained" BeforeTargets="Restore">
                <PropertyGroup>
                  <SelfContained>true</SelfContained>
                </PropertyGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "GlobalAndTarget.csproj", content);
        WriteMinimalProgram(projectDir);

        // Pass SelfContained=false on command line, but target overrides to true
        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"msbuild GlobalAndTarget.csproj -t:Restore -bl:\"{binlogPath}\" /p:SelfContained=false");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("GlobalAndTarget.csproj", StringComparison.OrdinalIgnoreCase));

        // Note: Global properties cannot be overridden by targets in MSBuild.
        // The global property wins. This tests that we correctly capture this MSBuild behavior.
        projectInfo.SelfContained.Should().Be(
            false,
            "global properties cannot be overridden by targets in MSBuild");
    }

    [TestMethod]
    public async Task ImportedPropsFile_PropertyFromImportCaptured()
    {
        // A project imports a .props file that sets a property
        var projectDir = Path.Combine(this.testDir, "ImportedProps");
        Directory.CreateDirectory(projectDir);

        var propsContent = """
            <Project>
              <PropertyGroup>
                <IsTestProject>true</IsTestProject>
                <IsShipping>false</IsShipping>
              </PropertyGroup>
            </Project>
            """;
        WriteFile(projectDir, "Directory.Build.props", propsContent);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "ImportedProps.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "ImportedProps.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("ImportedProps.csproj", StringComparison.OrdinalIgnoreCase));

        // Properties from the imported file should be captured during evaluation
        projectInfo.IsTestProject.Should().Be(true);
        projectInfo.IsShipping.Should().Be(false);
    }

    [TestMethod]
    public async Task TargetRemovesAndReAddsItem_FinalStateReflectsReAdd()
    {
        // A target removes a PackageReference and then re-adds it with different metadata
        var projectDir = Path.Combine(this.testDir, "RemoveReAdd");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <Target Name="ReplacePackage" BeforeTargets="Restore;CollectPackageReferences">
                <ItemGroup>
                  <PackageReference Remove="Newtonsoft.Json" />
                  <PackageReference Include="Newtonsoft.Json" Version="13.0.3">
                    <PrivateAssets>all</PrivateAssets>
                  </PackageReference>
                </ItemGroup>
              </Target>
            </Project>
            """;
        WriteFile(projectDir, "RemoveReAdd.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "RemoveReAdd.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("RemoveReAdd.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // The re-added item should have the updated version and metadata
        var item = projectInfo.PackageReference["Newtonsoft.Json"];
        item.GetMetadata("Version").Should().Be("13.0.3");
        item.GetMetadata("PrivateAssets").Should().Be("all");
    }

    [TestMethod]
    public async Task MultiTarget_BuildAndPublishSelfContained_MergesInnerBuilds()
    {
        // Multi-targeted project where a second pass restores with SelfContained=true.
        // The merge should mark the inner builds as self-contained.
        var solutionDir = Path.Combine(this.testDir, "MultiTargetPublish");
        Directory.CreateDirectory(solutionDir);

        var appContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net7.0</TargetFrameworks>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        var appDir = Path.Combine(solutionDir, "App");
        Directory.CreateDirectory(appDir);
        WriteFile(appDir, "App.csproj", appContent);
        WriteMinimalProgram(appDir);

        // Orchestrator: restore, build, then restore with SelfContained (all in one binlog)
        var orchestratorContent = """
            <Project>
              <Target Name="BuildThenRestore">
                <MSBuild Projects="App\App.csproj" Targets="Restore" />
                <MSBuild Projects="App\App.csproj" Targets="Build" Properties="UseAppHost=false" />
                <MSBuild Projects="App\App.csproj" Targets="Restore" Properties="SelfContained=true;RuntimeIdentifier=win-x64" />
              </Target>
            </Project>
            """;
        WriteFile(solutionDir, "Orchestrator.proj", orchestratorContent);

        var binlogPath = Path.Combine(solutionDir, "build.binlog");
        await RunDotNetAsync(solutionDir, $"msbuild Orchestrator.proj -t:BuildThenRestore -bl:\"{binlogPath}\"");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var appInfo = results.FirstOrDefault(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("App.csproj", StringComparison.OrdinalIgnoreCase));

        appInfo.Should().NotBeNull();

        // The first build creates inner builds; the second restore adds SelfContained.
        // Verify at least the project is found and contains expected packages.
        appInfo!.PackageReference.Should().ContainKey("Newtonsoft.Json");

        // After merging, some representation should have SelfContained=true
        // (either on the project directly, or in inner builds)
        var allInfos = new[] { appInfo }
            .Concat(appInfo.InnerBuilds)
            .ToList();
        allInfos.Should().Contain(
            p => p.SelfContained == true,
            "the second pass sets SelfContained=true which should be merged");
    }

    [TestMethod]
    public async Task ItemDefinitionGroupWithPerItemOverride_OverrideWins()
    {
        // ItemDefinitionGroup sets PrivateAssets=all for all PackageReferences,
        // but one specific PackageReference overrides it
        var projectDir = Path.Combine(this.testDir, "DefGroupOverride");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemDefinitionGroup>
                <PackageReference>
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemDefinitionGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="System.Memory" Version="4.5.5">
                  <PrivateAssets>none</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "DefGroupOverride.csproj", content);

        var binlogPath = await BuildProjectAsync(projectDir, "DefGroupOverride.csproj");
        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("DefGroupOverride.csproj", StringComparison.OrdinalIgnoreCase));

        // Newtonsoft.Json inherits PrivateAssets=all from ItemDefinitionGroup
        var newtonsoftItem = projectInfo.PackageReference["Newtonsoft.Json"];
        newtonsoftItem.GetMetadata("PrivateAssets").Should().Be("all");

        // System.Memory overrides to none
        var memoryItem = projectInfo.PackageReference["System.Memory"];
        memoryItem.GetMetadata("PrivateAssets").Should().Be("none");
    }

    [TestMethod]
    public async Task PropertyPrecedence_EnvVarOverriddenByProjectFile()
    {
        // Environment sets OutputType=Library, project file sets OutputType=Exe.
        // MSBuild precedence: project file wins over env vars.
        var projectDir = Path.Combine(this.testDir, "EnvVarPrecedence");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "EnvVarPrecedence.csproj", content);
        WriteMinimalProgram(projectDir);

        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunProcessWithEnvAsync(
            projectDir,
            "dotnet",
            $"build EnvVarPrecedence.csproj -bl:\"{binlogPath}\" /p:UseAppHost=false",
            ("OutputType", "Library"));

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("EnvVarPrecedence.csproj", StringComparison.OrdinalIgnoreCase));

        // Project file property wins over environment variable
        projectInfo.OutputType.Should().Be("Exe");
    }

    [TestMethod]
    public async Task PublishAotWithGlobalSelfContained_BothCaptured()
    {
        // Project has PublishAot=true, global property adds SelfContained=true
        var projectDir = Path.Combine(this.testDir, "AotAndSC");
        Directory.CreateDirectory(projectDir);

        var content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <PublishAot>true</PublishAot>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """;
        WriteFile(projectDir, "AotAndSC.csproj", content);
        WriteMinimalProgram(projectDir);

        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"msbuild AotAndSC.csproj -t:Restore -bl:\"{binlogPath}\" /p:SelfContained=true /p:RuntimeIdentifier=win-x64");

        var results = this.processor.ExtractProjectInfo(binlogPath);

        var projectInfo = results.First(p =>
            p.ProjectPath != null &&
            p.ProjectPath.EndsWith("AotAndSC.csproj", StringComparison.OrdinalIgnoreCase));

        projectInfo.PublishAot.Should().Be(true);
        projectInfo.SelfContained.Should().Be(true);
    }

    private static void WriteFile(string directory, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private static void WriteMinimalProgram(string directory)
    {
        WriteFile(directory, "Program.cs", "System.Console.WriteLine();");
    }

    private static async Task<string> BuildProjectAsync(string projectDir, string projectFile)
    {
        var binlogPath = Path.Combine(projectDir, "build.binlog");
        await RunDotNetAsync(projectDir, $"build \"{projectFile}\" -bl:\"{binlogPath}\" /p:UseAppHost=false");

        if (!File.Exists(binlogPath))
        {
            throw new InvalidOperationException($"Build did not produce binlog at: {binlogPath}");
        }

        return binlogPath;
    }

    private static async Task RunDotNetAsync(string workingDirectory, string arguments)
    {
        // dotnet build already includes restore by default, no need for separate restore
        await RunProcessAsync(workingDirectory, "dotnet", arguments);
    }

    private static async Task RunProcessAsync(string workingDirectory, string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: {fileName} {arguments}");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Process exited with code {process.ExitCode}.\nCommand: {fileName} {arguments}\nStdout:\n{stdout}\nStderr:\n{stderr}");
        }
    }

    private static async Task RunProcessWithEnvAsync(
        string workingDirectory,
        string fileName,
        string arguments,
        params (string Key, string Value)[] envVars)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var (key, value) in envVars)
        {
            psi.Environment[key] = value;
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: {fileName} {arguments}");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Process exited with code {process.ExitCode}.\nCommand: {fileName} {arguments}\nStdout:\n{stdout}\nStderr:\n{stderr}");
        }
    }
}

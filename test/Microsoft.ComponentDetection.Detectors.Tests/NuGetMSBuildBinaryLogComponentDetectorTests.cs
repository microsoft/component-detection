﻿namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;
using Task = System.Threading.Tasks.Task;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetMSBuildBinaryLogComponentDetectorTests : BaseDetectorTest<NuGetMSBuildBinaryLogComponentDetector>
{
    static NuGetMSBuildBinaryLogComponentDetectorTests() => NuGetMSBuildBinaryLogComponentDetector.EnsureMSBuildIsRegistered();

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
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        originalFileComponents.Should().Equal("Some.Package/1.2.3", "Transitive.Dependency/4.5.6");

        var referencedFileComponents = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/other-project/other-project.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
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

        var packages = detectedComponents
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        packages.Should().Equal("Some.Package/1.2.3");
    }

    [TestMethod]
    public async Task PackagesReportedFromSeparateProjectsDoNotOverlap()
    {
        // In this test, a top-level solution file references two projects which have no relationship between them.
        // The result should be that each project only reports its own dependencies.
        var slnContents = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 17
VisualStudioVersion = 17.0.31808.319
MinimumVisualStudioVersion = 15.0.26124.0
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""project1"", ""project1\project1.csproj"", ""{782E0C0A-10D3-444D-9640-263D03D2B20C}""
EndProject
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""project2"", ""project2\project2.csproj"", ""{CBA73BF8-C922-4DD7-A41D-88CD22914356}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Release|Any CPU.Build.0 = Release|Any CPU
		{CBA73BF8-C922-4DD7-A41D-88CD22914356}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{CBA73BF8-C922-4DD7-A41D-88CD22914356}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{CBA73BF8-C922-4DD7-A41D-88CD22914356}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{CBA73BF8-C922-4DD7-A41D-88CD22914356}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";
        using var binLogStream = await MSBuildTestUtilities.GetBinLogStreamFromFileContentsAsync(
            defaultFilePath: "solution.sln",
            defaultFileContents: slnContents,
            additionalFiles: new[]
                {
                    ("project1/project1.csproj", $@"
                        <Project Sdk=""Microsoft.NET.Sdk"">
                          <PropertyGroup>
                            <TargetFramework>{MSBuildTestUtilities.TestTargetFramework}</TargetFramework>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageReference Include=""Package.A"" Version=""1.2.3"" />
                          </ItemGroup>
                        </Project>
                        "),
                    ("project2/project2.csproj", $@"
                        <Project Sdk=""Microsoft.NET.Sdk"">
                          <PropertyGroup>
                            <TargetFramework>{MSBuildTestUtilities.TestTargetFramework}</TargetFramework>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageReference Include=""Package.B"" Version=""4.5.6"" />
                          </ItemGroup>
                        </Project>
                        "),
                },
            mockedPackages: new[]
                {
                    ("Package.A", "1.2.3", MSBuildTestUtilities.TestTargetFramework, "<dependencies />"),
                    ("Package.B", "4.5.6", MSBuildTestUtilities.TestTargetFramework, "<dependencies />"),
                });
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("msbuild.binlog", binLogStream)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        var project1Components = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/project1.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        project1Components.Should().Equal("Package.A/1.2.3");

        var project2Components = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/project2.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        project2Components.Should().Equal("Package.B/4.5.6");

        var solutionComponents = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/solution.sln")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        solutionComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task PackagesAreReportedWhenConditionedOnTargetFramework()
    {
        // This test simulates a project with multiple TFMs where the same package is imported in each, but with a
        // different version.  To avoid building the entire project, we can fake what MSBuild does by resolving
        // packages with each TFM.  I manually verified that a "real" invocation of MSBuild with the "Build" target
        // produces the same shape of the binlog as this test generates.

        // The end result is that _all_ packages are reported, regardless of the TFM invokation they came from, and in
        // this case that is good, because we really only care about what packages were used in the build and what
        // project file they came from.
        var (scanResult, componentRecorder) = await this.ExecuteDetectorAndGetBinLogAsync(
            projectContents: $@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                  <PropertyGroup>
                    <TargetFrameworks>netstandard2.0;{MSBuildTestUtilities.TestTargetFramework}</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include=""Some.Package"" Version=""1.2.3"" Condition=""'$(TargetFramework)' == 'netstandard2.0'"" />
                    <PackageReference Include=""Some.Package"" Version=""4.5.6"" Condition=""'$(TargetFramework)' == '{MSBuildTestUtilities.TestTargetFramework}'"" />
                  </ItemGroup>
                  <Target Name=""TEST_GenerateBuildDependencyFileForTargetFrameworks"">
                    <MSBuild Projects=""$(MSBuildThisFile)"" Properties=""TargetFramework=netstandard2.0"" Targets=""GenerateBuildDependencyFile"" />
                    <MSBuild Projects=""$(MSBuildThisFile)"" Properties=""TargetFramework={MSBuildTestUtilities.TestTargetFramework}"" Targets=""GenerateBuildDependencyFile"" />
                  </Target>
                </Project>
                ",
            targetName: "TEST_GenerateBuildDependencyFileForTargetFrameworks",
            mockedPackages: new[]
                {
                    ("NETStandard.Library", "2.0.3", "netstandard2.0", "<dependencies />"),
                    ("Some.Package", "1.2.3", "netstandard2.0", null),
                    ("Some.Package", "4.5.6", MSBuildTestUtilities.TestTargetFramework, null),
                });

        var detectedComponents = componentRecorder.GetDetectedComponents();

        var packages = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/project.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
            .OrderBy(c => c.Name).ThenBy(c => c.Version)
            .Select(c => $"{c.Name}/{c.Version}");
        packages.Should().Equal("NETStandard.Library/2.0.3", "Some.Package/1.2.3", "Some.Package/4.5.6");
    }

    [TestMethod]
    public async Task PackagesAreReportedWhenConflictResolutionRemovesPackages()
    {
        // This comes from a real-world scenario where the project file looks like this:
        //
        //   <Project Sdk="Microsoft.NET.Sdk">
        //     <PropertyGroup>
        //       <TargetFrameworks>net472;net8.0</TargetFrameworks>
        //     </PropertyGroup>
        //     <ItemGroup>
        //       <PackageReference Include="System.Text.Json" Version="6.0.0" />
        //     </ItemGroup>
        //   </Project>
        //
        // During a build, the project is evantually evaluated twice: once for `net472` and once for `net8.0`.  In the
        // `net472` scenario, `System.Text.Json/6.0.0` is added to the output, but in the `net8.0` scenario, that
        // package is added but then removed by the conflict resolution logic in the SDK.  We need to ensure that the
        // `RemoveItem` from the `net8.0` evaluation doesn't affect the `net472` evaluation.  The end result is that we
        // _should_ report the package because it was used in at least one project evaluation.
        //
        // To make this easy to test, custom targets have been added to the test project below that do exactly what the
        // SDK does, just without an expernsive build invocation; remove an item from the item group, but only for one
        // of the target frameworks.  The end result is a binary log that mimics the shape of the real-world example
        // given above.
        var (scanResult, componentRecorder) = await this.ExecuteDetectorAndGetBinLogAsync(
            projectContents: $@"
                <Project Sdk=""Microsoft.NET.Sdk"">
                  <PropertyGroup>
                    <TargetFrameworks>netstandard2.0;{MSBuildTestUtilities.TestTargetFramework}</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include=""Some.Package"" Version=""1.2.3"" />
                  </ItemGroup>
                  <Target Name=""TEST_GenerateBuildDependencyFile"" DependsOnTargets=""GenerateBuildDependencyFile"">
                    <ItemGroup>
                      <RuntimeCopyLocalItems Remove=""$(NUGET_PACKAGES)/**/Some.Package.dll""
                                             NuGetPackageId=""Some.Package""
                                             NuGetPackageVersion=""1.2.3""
                                             Condition=""'$(TargetFramework)' == '{MSBuildTestUtilities.TestTargetFramework}'"" />
                    </ItemGroup>
                  </Target>
                  <Target Name=""TEST_GenerateBuildDependencyFileForTargetFrameworks"">
                    <MSBuild Projects=""$(MSBuildThisFile)"" Properties=""TargetFramework=netstandard2.0"" Targets=""TEST_GenerateBuildDependencyFile"" />
                    <MSBuild Projects=""$(MSBuildThisFile)"" Properties=""TargetFramework={MSBuildTestUtilities.TestTargetFramework}"" Targets=""TEST_GenerateBuildDependencyFile"" />
                  </Target>
                </Project>
                ",
            targetName: "TEST_GenerateBuildDependencyFileForTargetFrameworks",
            mockedPackages: new[]
                {
                    ("NETStandard.Library", "2.0.3", "netstandard2.0", "<dependencies />"),
                    ("Some.Package", "1.2.3", "netstandard2.0", null),
                });

        var detectedComponents = componentRecorder.GetDetectedComponents();

        var packages = detectedComponents
            .Where(d => d.FilePaths.Any(p => p.Replace("\\", "/").EndsWith("/project.csproj")))
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .Where(c => c.Name != ".NET SDK") // dealt with in another test because the SDK version will change regularly
            .OrderBy(c => c.Name).ThenBy(c => c.Version)
            .Select(c => $"{c.Name}/{c.Version}");
        packages.Should().Equal("NETStandard.Library/2.0.3", "Some.Package/1.2.3");
    }

    [TestMethod]
    public async Task PackagesImplicitlyAddedBySdkDuringPublishAreAdded()
    {
        // When a project is published, the SDK will add references to some AppHost specific packages.  Doing an actual
        // publish operation here would be too slow, so a mock in-memory binary log is used that has the same shape
        // (although trimmed down) of a real publish log.
        var binlog = new Build()
        {
            Succeeded = true,
            Children =
            {
                new Project()
                {
                    ProjectFile = "project.csproj",
                    Children =
                    {
                        new Target()
                        {
                            Name = "ResolveFrameworkReferences",
                            Children =
                            {
                                // ResolvedAppHostPack
                                new MSBuildTask()
                                {
                                    Name = "GetPackageDirectory",
                                    Children =
                                    {
                                        new Folder()
                                        {
                                            Name = "OutputItems",
                                            Children =
                                            {
                                                new AddItem()
                                                {
                                                    Name = "ResolvedAppHostPack",
                                                    Children =
                                                    {
                                                        new Item()
                                                        {
                                                            Name = "AppHost",
                                                            Children =
                                                            {
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageId",
                                                                    Value = "Microsoft.NETCore.App.Host.win-x64",
                                                                },
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageVersion",
                                                                    Value = "6.0.33",
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },

                                // ResolvedSingleFileHostPack
                                new MSBuildTask()
                                {
                                    Name = "GetPackageDirectory",
                                    Children =
                                    {
                                        new Folder()
                                        {
                                            Name = "OutputItems",
                                            Children =
                                            {
                                                new AddItem()
                                                {
                                                    Name = "ResolvedSingleFileHostPack",
                                                    Children =
                                                    {
                                                        new Item()
                                                        {
                                                            Name = "SingleFileHost",
                                                            Children =
                                                            {
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageId",
                                                                    Value = "Microsoft.NETCore.App.Host.win-x64",
                                                                },
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageVersion",
                                                                    Value = "6.0.33",
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },

                                // ResolvedComHostPack
                                new MSBuildTask()
                                {
                                    Name = "GetPackageDirectory",
                                    Children =
                                    {
                                        new Folder()
                                        {
                                            Name = "OutputItems",
                                            Children =
                                            {
                                                new AddItem()
                                                {
                                                    Name = "ResolvedComHostPack",
                                                    Children =
                                                    {
                                                        new Item()
                                                        {
                                                            Name = "ComHost",
                                                            Children =
                                                            {
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageId",
                                                                    Value = "Microsoft.NETCore.App.Host.win-x64",
                                                                },
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageVersion",
                                                                    Value = "6.0.33",
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },

                                // ResolvedIjwHostPack
                                new MSBuildTask()
                                {
                                    Name = "GetPackageDirectory",
                                    Children =
                                    {
                                        new Folder()
                                        {
                                            Name = "OutputItems",
                                            Children =
                                            {
                                                new AddItem()
                                                {
                                                    Name = "ResolvedIjwHostPack",
                                                    Children =
                                                    {
                                                        new Item()
                                                        {
                                                            Name = "IjwHost",
                                                            Children =
                                                            {
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageId",
                                                                    Value = "Microsoft.NETCore.App.Host.win-x64",
                                                                },
                                                                new Metadata()
                                                                {
                                                                    Name = "NuGetPackageVersion",
                                                                    Value = "6.0.33",
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        // in-memory logs need to be `.buildlog`
        using var tempFile = new TemporaryFile(".buildlog");
        Serialization.Write(binlog, tempFile.FilePath);
        using var binLogStream = File.OpenRead(tempFile.FilePath);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(tempFile.FilePath, binLogStream)
            .ExecuteDetectorAsync();
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var components = detectedComponents
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        components.Should().Equal("Microsoft.NETCore.App.Host.win-x64/6.0.33");
    }

    [TestMethod]
    public async Task DotNetSDKVersionIsReported()
    {
        var binlog = new Build()
        {
            Succeeded = true,
        };

        binlog.EvaluationFolder.Children.Add(
            new Project()
            {
                ProjectFile = "project.csproj",
                Children =
                {
                    new Folder()
                    {
                        Name = "Properties",
                        Children =
                        {
                            new Property()
                            {
                                Name = "NETCoreSdkVersion",
                                Value = "6.0.789",
                            },
                        },
                    },
                },
            });

        // in-memory logs need to be `.buildlog`
        using var tempFile = new TemporaryFile(".buildlog");
        Serialization.Write(binlog, tempFile.FilePath);
        using var binLogStream = File.OpenRead(tempFile.FilePath);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(tempFile.FilePath, binLogStream)
            .ExecuteDetectorAsync();
        var detectedComponents = componentRecorder.GetDetectedComponents();

        var components = detectedComponents
            .Select(d => d.Component)
            .Cast<NuGetComponent>()
            .OrderBy(c => c.Name)
            .Select(c => $"{c.Name}/{c.Version}");
        components.Should().Equal(".NET SDK/6.0.789");
    }

    private async Task<(IndividualDetectorScanResult ScanResult, IComponentRecorder ComponentRecorder)> ExecuteDetectorAndGetBinLogAsync(
        string projectContents,
        string targetName = null,
        (string FileName, string Content)[] additionalFiles = null,
        (string Name, string Version, string TargetFramework, string DependenciesXml)[] mockedPackages = null)
    {
        using var binLogStream = await MSBuildTestUtilities.GetBinLogStreamFromFileContentsAsync("project.csproj", projectContents, targetName: targetName, additionalFiles: additionalFiles, mockedPackages: mockedPackages);
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("msbuild.binlog", binLogStream)
            .ExecuteDetectorAsync();
        return (scanResult, componentRecorder);
    }
}

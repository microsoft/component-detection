namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Build.Framework;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for <see cref="MSBuildBinaryLogComponentDetector"/>.
/// Fallback tests use <see cref="DetectorTestUtilityBuilder{T}"/> (public constructor, no binlog mock).
/// Binlog-enhanced tests construct the detector manually via the internal constructor with a mocked
/// <see cref="IBinLogProcessor"/>.
/// </summary>
[TestClass]
public class MSBuildBinaryLogComponentDetectorTests : BaseDetectorTest<MSBuildBinaryLogComponentDetector>
{
    private const string ProjectPath = @"C:\test\TestProject.csproj";
    private const string AssetsFilePath = @"C:\test\obj\project.assets.json";
    private const string BinlogFilePath = @"C:\test\build.binlog";

    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;

    public MSBuildBinaryLogComponentDetectorTests()
    {
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
    }

    // ================================================================
    // Fallback tests – no binlog info available.
    // Uses DetectorTestUtility / BaseDetectorTest (public constructor).
    // ================================================================
    [TestMethod]
    public async Task Fallback_SimpleAssetsFile_DetectsComponents()
    {
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", assetsJson)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = recorder.GetDetectedComponents();
        components.Should().HaveCount(1);

        var nuget = (NuGetComponent)components.Single().Component;
        nuget.Name.Should().Be("Newtonsoft.Json");
        nuget.Version.Should().Be("13.0.1");
    }

    [TestMethod]
    public async Task Fallback_TransitiveDependencies_BuildsDependencyGraph()
    {
        var assetsJson = TransitiveAssetsJson();

        var (result, recorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", assetsJson)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = recorder.GetDetectedComponents();
        components.Should().HaveCount(2);

        var graphs = recorder.GetDependencyGraphsByLocation();
        graphs.Should().NotBeEmpty();
        var graph = graphs.Values.First();

        var logging = components.First(c => ((NuGetComponent)c.Component).Name == "Microsoft.Extensions.Logging");
        var abstractions = components.First(c => ((NuGetComponent)c.Component).Name == "Microsoft.Extensions.Logging.Abstractions");

        graph.IsComponentExplicitlyReferenced(logging.Component.Id).Should().BeTrue();
        graph.IsComponentExplicitlyReferenced(abstractions.Component.Id).Should().BeFalse();
        graph.GetDependenciesForComponent(logging.Component.Id).Should().Contain(abstractions.Component.Id);
    }

    [TestMethod]
    public async Task Fallback_NoPackageSpec_HandlesGracefully()
    {
        var assetsJson = @"{ ""version"": 3, ""targets"": { ""net8.0"": {} }, ""libraries"": {}, ""packageFolders"": {} }";

        var (result, recorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", assetsJson)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Fallback_ProjectReference_ExcludesProjectDependencies()
    {
        var assetsJson = ProjectReferenceAssetsJson();

        var (result, recorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", assetsJson)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = recorder.GetDetectedComponents();
        components.Should().HaveCount(1);
        ((NuGetComponent)components.Single().Component).Name.Should().Be("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task Fallback_PackageDownload_RegisteredAsDevDependency()
    {
        var assetsJson = PackageDownloadAssetsJson();

        var (result, recorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", assetsJson)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var download = recorder.GetDetectedComponents()
            .Single(c => ((NuGetComponent)c.Component).Name == "Microsoft.Net.Compilers.Toolset");
        recorder.GetEffectiveDevDependencyValue(download.Component.Id).Should().BeTrue();
    }

    // ================================================================
    // Binlog-enhanced tests – mock IBinLogProcessor injected via
    // the internal constructor (accessible through InternalsVisibleTo).
    // ================================================================
    [TestMethod]
    public async Task WithBinlog_NormalProject_DetectsNuGetComponents()
    {
        var info = CreateProjectInfo();
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var nuget = recorder.GetDetectedComponents()
            .Where(c => c.Component is NuGetComponent)
            .ToList();
        nuget.Should().HaveCount(1);
        ((NuGetComponent)nuget[0].Component).Name.Should().Be("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task WithBinlog_TestProject_AllDependenciesMarkedAsDev()
    {
        var info = CreateProjectInfo();
        info.IsTestProject = true;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_IsShippingFalse_AllDependenciesMarkedAsDev()
    {
        var info = CreateProjectInfo();
        info.IsShipping = false;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_IsDevelopmentTrue_AllDependenciesMarkedAsDev()
    {
        var info = CreateProjectInfo();
        info.IsDevelopment = true;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_ShippingProject_DependenciesNotMarkedAsDev()
    {
        // All dev-only flags are null or positive, so this is a normal shipping project
        var info = CreateProjectInfo();
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithBinlog_ExplicitDevDependencyTrue_OverridesPackageToDev()
    {
        var info = CreateProjectInfo();
        AddPackageReference(info, "Newtonsoft.Json", isDevelopmentDependency: true);
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_ExplicitDevDependencyFalse_OverridesPackageToNotDev()
    {
        // On a normal project, IsDevelopmentDependency=false means "not dev", even when heuristics
        // (framework component / autoReferenced) would otherwise classify it as dev.
        var info = CreateProjectInfo();
        AddPackageReference(info, "Newtonsoft.Json", isDevelopmentDependency: false);
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithBinlog_TestProject_OverridesPerPackageFalse()
    {
        // Project-level classification (IsTestProject) always wins.
        // IsDevelopmentDependency=false on a package is ignored because the project IS a test project.
        var info = CreateProjectInfo();
        info.IsTestProject = true;
        AddPackageReference(info, "Newtonsoft.Json", isDevelopmentDependency: false);
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_TransitiveDepsOfDevPackage_InheritDevStatus()
    {
        // When a top-level package has IsDevelopmentDependency=true, the override callback
        // (_ => true) applies transitively to the entire sub-graph.
        var info = CreateProjectInfo();
        AddPackageReference(info, "Microsoft.Extensions.Logging", isDevelopmentDependency: true);
        var assetsJson = TransitiveAssetsJson();

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = recorder.GetDetectedComponents().Where(c => c.Component is NuGetComponent).ToList();
        components.Should().HaveCount(2);

        var abstractions = components.First(c => ((NuGetComponent)c.Component).Name == "Microsoft.Extensions.Logging.Abstractions");
        recorder.GetEffectiveDevDependencyValue(abstractions.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_PackageDownload_DefaultIsDevDependency()
    {
        var info = CreateProjectInfo();
        var assetsJson = PackageDownloadAssetsJson();

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var download = recorder.GetDetectedComponents()
            .First(c => ((NuGetComponent)c.Component).Name == "Microsoft.Net.Compilers.Toolset");
        recorder.GetEffectiveDevDependencyValue(download.Component.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task WithBinlog_PackageDownload_ExplicitFalse_NotDevDependency()
    {
        var info = CreateProjectInfo();
        AddPackageDownload(info, "Microsoft.Net.Compilers.Toolset", isDevelopmentDependency: false);
        var assetsJson = PackageDownloadAssetsJson();

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var download = recorder.GetDetectedComponents()
            .First(c => ((NuGetComponent)c.Component).Name == "Microsoft.Net.Compilers.Toolset");
        recorder.GetEffectiveDevDependencyValue(download.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithBinlog_RegistersDotNetComponent()
    {
        var info = CreateProjectInfo();
        info.NETCoreSdkVersion = "8.0.100";
        info.OutputType = "Library";
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var dotNetComponents = recorder.GetDetectedComponents()
            .Where(c => c.Component is DotNetComponent)
            .ToList();
        dotNetComponents.Should().HaveCount(1);

        var dotNet = (DotNetComponent)dotNetComponents[0].Component;
        dotNet.SdkVersion.Should().Be("8.0.100");
        dotNet.TargetFramework.Should().Be("net8.0");
        dotNet.ProjectType.Should().Be("library");
    }

    [TestMethod]
    public async Task WithBinlog_SelfContained_RegistersApplicationSelfContained()
    {
        var info = CreateProjectInfo();
        info.NETCoreSdkVersion = "8.0.100";
        info.OutputType = "Exe";
        info.SelfContained = true;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var dotNet = recorder.GetDetectedComponents()
            .Where(c => c.Component is DotNetComponent)
            .Select(c => (DotNetComponent)c.Component)
            .Single();
        dotNet.ProjectType.Should().Be("application-selfcontained");
    }

    [TestMethod]
    public async Task WithBinlog_PublishAot_RegistersApplicationSelfContained()
    {
        var info = CreateProjectInfo();
        info.NETCoreSdkVersion = "8.0.100";
        info.OutputType = "Exe";
        info.PublishAot = true;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var dotNet = recorder.GetDetectedComponents()
            .Where(c => c.Component is DotNetComponent)
            .Select(c => (DotNetComponent)c.Component)
            .Single();
        dotNet.ProjectType.Should().Be("application-selfcontained");
    }

    [TestMethod]
    public async Task WithBinlog_LibrarySelfContained_RegistersLibrarySelfContained()
    {
        var info = CreateProjectInfo();
        info.NETCoreSdkVersion = "8.0.100";
        info.OutputType = "Library";
        info.SelfContained = true;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var dotNet = recorder.GetDetectedComponents()
            .Where(c => c.Component is DotNetComponent)
            .Select(c => (DotNetComponent)c.Component)
            .Single();
        dotNet.ProjectType.Should().Be("library-selfcontained");
    }

    [TestMethod]
    public async Task WithBinlog_NotSelfContained_RegistersPlainApplication()
    {
        var info = CreateProjectInfo();
        info.NETCoreSdkVersion = "8.0.100";
        info.OutputType = "Exe";
        info.SelfContained = false;
        info.PublishAot = false;
        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([info], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var dotNet = recorder.GetDetectedComponents()
            .Where(c => c.Component is DotNetComponent)
            .Select(c => (DotNetComponent)c.Component)
            .Single();
        dotNet.ProjectType.Should().Be("application");
    }

    [TestMethod]
    public async Task WithBinlog_MultiTarget_PerTfmDevDependency()
    {
        // Multi-target project: net8.0 inner build marks package as dev, net6.0 does not.
        var outer = CreateProjectInfo(targetFramework: null);
        outer.TrySetProperty("TargetFrameworks", "net8.0;net6.0");

        var innerNet8 = new MSBuildProjectInfo { ProjectPath = ProjectPath, ProjectAssetsFile = AssetsFilePath, TargetFramework = "net8.0" };
        AddPackageReference(innerNet8, "Newtonsoft.Json", isDevelopmentDependency: true);

        var innerNet6 = new MSBuildProjectInfo { ProjectPath = ProjectPath, ProjectAssetsFile = AssetsFilePath, TargetFramework = "net6.0" };

        outer.InnerBuilds.Add(innerNet8);
        outer.InnerBuilds.Add(innerNet6);

        var assetsJson = MultiTargetAssetsJson();

        var (result, recorder) = await ExecuteWithBinlogAsync([outer], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Package appears in both TFMs.
        // net8.0: devDependencyOverride = true → registered as dev
        // net6.0: devDependencyOverride = null → uses heuristic → normal package → NOT dev
        // GetEffectiveDevDependencyValue ANDs across registrations → false wins
        var component = recorder.GetDetectedComponents()
            .Single(c => c.Component is NuGetComponent && ((NuGetComponent)c.Component).Name == "Newtonsoft.Json");
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task WithBinlog_NoBinlogMatch_FallsBackToStandardProcessing()
    {
        // Binlog contains info for a DIFFERENT project; assets file project path doesn't match.
        var otherInfo = CreateProjectInfo(
            projectPath: @"C:\other\OtherProject.csproj",
            assetsFilePath: @"C:\other\obj\project.assets.json");
        otherInfo.IsTestProject = true;

        var assetsJson = SimpleAssetsJson("Newtonsoft.Json", "13.0.1");

        var (result, recorder) = await ExecuteWithBinlogAsync([otherInfo], assetsJson);

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Single(c => c.Component is NuGetComponent);

        // Falls back to standard processing – no dev-dependency override
        recorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().BeFalse();
    }

    // ================================================================
    // Helpers – project info construction
    // ================================================================
    private static MSBuildProjectInfo CreateProjectInfo(
        string projectPath = ProjectPath,
        string assetsFilePath = AssetsFilePath,
        string? targetFramework = "net8.0")
    {
        return new MSBuildProjectInfo
        {
            ProjectPath = projectPath,
            ProjectAssetsFile = assetsFilePath,
            TargetFramework = targetFramework,
        };
    }

    private static void AddPackageReference(MSBuildProjectInfo info, string packageName, bool isDevelopmentDependency)
    {
        var item = CreateTaskItemMock(packageName, isDevelopmentDependency);
        info.PackageReference[packageName] = item;
    }

    private static void AddPackageDownload(MSBuildProjectInfo info, string packageName, bool isDevelopmentDependency)
    {
        var item = CreateTaskItemMock(packageName, isDevelopmentDependency);
        info.PackageDownload[packageName] = item;
    }

    private static ITaskItem CreateTaskItemMock(string itemSpec, bool isDevelopmentDependency)
    {
        var mock = new Mock<ITaskItem>();
        mock.SetupGet(x => x.ItemSpec).Returns(itemSpec);
        mock.Setup(x => x.GetMetadata("IsDevelopmentDependency"))
            .Returns(isDevelopmentDependency ? "true" : "false");
        return mock.Object;
    }

    // ================================================================
    // Helpers – detector execution with mocked IBinLogProcessor
    // ================================================================
    private static async Task<(IndividualDetectorScanResult Result, IComponentRecorder Recorder)> ExecuteWithBinlogAsync(
        IReadOnlyList<MSBuildProjectInfo> projectInfos,
        string assetsJson,
        string binlogPath = BinlogFilePath,
        string assetsLocation = AssetsFilePath)
    {
        var binLogProcessorMock = new Mock<IBinLogProcessor>();
        binLogProcessorMock
            .Setup(x => x.ExtractProjectInfo(binlogPath))
            .Returns(projectInfos);

        var walkerMock = new Mock<IObservableDirectoryWalkerFactory>();
        var streamFactoryMock = new Mock<IComponentStreamEnumerableFactory>();
        var fileUtilityMock = new Mock<IFileUtilityService>();
        fileUtilityMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        var loggerMock = new Mock<ILogger<MSBuildBinaryLogComponentDetector>>();

        var detector = new MSBuildBinaryLogComponentDetector(
            streamFactoryMock.Object,
            walkerMock.Object,
            fileUtilityMock.Object,
            binLogProcessorMock.Object,
            loggerMock.Object);

        var recorder = new ComponentRecorder();

        var requests = new[]
        {
            CreateProcessRequest(recorder, binlogPath, "fake-binlog-content"),
            CreateProcessRequest(recorder, assetsLocation, assetsJson),
        };

        walkerMock
            .Setup(x => x.GetFilteredComponentStreamObservable(
                It.IsAny<DirectoryInfo>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IComponentRecorder>()))
            .Returns(requests.ToObservable());

        var scanRequest = new ScanRequest(
            new DirectoryInfo(Path.GetTempPath()),
            null,
            null,
            new Dictionary<string, string>(),
            null,
            recorder,
            sourceFileRoot: new DirectoryInfo(Path.GetTempPath()));

        var result = await detector.ExecuteDetectorAsync(scanRequest);
        return (result, recorder);
    }

    private static ProcessRequest CreateProcessRequest(IComponentRecorder recorder, string location, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var mockStream = new Mock<IComponentStream>();
        mockStream.SetupGet(x => x.Stream).Returns(stream);
        mockStream.SetupGet(x => x.Location).Returns(location);
        mockStream.SetupGet(x => x.Pattern).Returns(Path.GetFileName(location));

        return new ProcessRequest
        {
            SingleFileComponentRecorder = recorder.CreateSingleFileComponentRecorder(location),
            ComponentStream = mockStream.Object,
        };
    }

    /// <summary>
    /// Creates a minimal valid project.assets.json with a single package.
    /// </summary>
    private static string SimpleAssetsJson(string packageName, string version) => $$"""
        {
            "version": 3,
            "targets": {
                "net8.0": {
                    "{{packageName}}/{{version}}": {
                        "type": "package",
                        "compile": { "lib/net8.0/{{packageName}}.dll": {} },
                        "runtime": { "lib/net8.0/{{packageName}}.dll": {} }
                    }
                }
            },
            "libraries": {
                "{{packageName}}/{{version}}": {
                    "sha512": "fakehash",
                    "type": "package",
                    "path": "{{packageName.ToUpperInvariant()}}/{{version}}",
                    "files": [ "lib/net8.0/{{packageName}}.dll" ]
                }
            },
            "projectFileDependencyGroups": {
                "net8.0": [ "{{packageName}} >= {{version}}" ]
            },
            "packageFolders": { "C:\\Users\\test\\.nuget\\packages\\": {} },
            "project": {
                "version": "1.0.0",
                "restore": {
                    "projectName": "TestProject",
                    "projectPath": "C:\\test\\TestProject.csproj",
                    "outputPath": "C:\\test\\obj"
                },
                "frameworks": {
                    "net8.0": {
                        "targetAlias": "net8.0",
                        "dependencies": {
                            "{{packageName}}": { "target": "Package", "version": "[{{version}}, )" }
                        }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// Assets JSON with a top-level package that has a transitive dependency.
    /// Microsoft.Extensions.Logging → Microsoft.Extensions.Logging.Abstractions.
    /// </summary>
    private static string TransitiveAssetsJson() => """
        {
            "version": 3,
            "targets": {
                "net8.0": {
                    "Microsoft.Extensions.Logging/8.0.0": {
                        "type": "package",
                        "dependencies": { "Microsoft.Extensions.Logging.Abstractions": "8.0.0" },
                        "compile": { "lib/net8.0/Microsoft.Extensions.Logging.dll": {} },
                        "runtime": { "lib/net8.0/Microsoft.Extensions.Logging.dll": {} }
                    },
                    "Microsoft.Extensions.Logging.Abstractions/8.0.0": {
                        "type": "package",
                        "compile": { "lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll": {} },
                        "runtime": { "lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll": {} }
                    }
                }
            },
            "libraries": {
                "Microsoft.Extensions.Logging/8.0.0": {
                    "sha512": "fakehash", "type": "package",
                    "path": "microsoft.extensions.logging/8.0.0",
                    "files": [ "lib/net8.0/Microsoft.Extensions.Logging.dll" ]
                },
                "Microsoft.Extensions.Logging.Abstractions/8.0.0": {
                    "sha512": "fakehash", "type": "package",
                    "path": "microsoft.extensions.logging.abstractions/8.0.0",
                    "files": [ "lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll" ]
                }
            },
            "projectFileDependencyGroups": {
                "net8.0": [ "Microsoft.Extensions.Logging >= 8.0.0" ]
            },
            "packageFolders": { "C:\\Users\\test\\.nuget\\packages\\": {} },
            "project": {
                "version": "1.0.0",
                "restore": {
                    "projectName": "TestProject",
                    "projectPath": "C:\\test\\TestProject.csproj",
                    "outputPath": "C:\\test\\obj"
                },
                "frameworks": {
                    "net8.0": {
                        "targetAlias": "net8.0",
                        "dependencies": {
                            "Microsoft.Extensions.Logging": { "target": "Package", "version": "[8.0.0, )" }
                        }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// Assets JSON with a NuGet package and a project reference.
    /// </summary>
    private static string ProjectReferenceAssetsJson() => """
        {
            "version": 3,
            "targets": {
                "net8.0": {
                    "Newtonsoft.Json/13.0.1": {
                        "type": "package",
                        "compile": { "lib/net8.0/Newtonsoft.Json.dll": {} },
                        "runtime": { "lib/net8.0/Newtonsoft.Json.dll": {} }
                    },
                    "MyOtherProject/1.0.0": { "type": "project" }
                }
            },
            "libraries": {
                "Newtonsoft.Json/13.0.1": {
                    "sha512": "fakehash", "type": "package",
                    "path": "newtonsoft.json/13.0.1",
                    "files": [ "lib/net8.0/Newtonsoft.Json.dll" ]
                },
                "MyOtherProject/1.0.0": {
                    "type": "project",
                    "path": "../MyOtherProject/MyOtherProject.csproj",
                    "msbuildProject": "../MyOtherProject/MyOtherProject.csproj"
                }
            },
            "projectFileDependencyGroups": {
                "net8.0": [ "Newtonsoft.Json >= 13.0.1" ]
            },
            "packageFolders": { "C:\\Users\\test\\.nuget\\packages\\": {} },
            "project": {
                "version": "1.0.0",
                "restore": {
                    "projectName": "TestProject",
                    "projectPath": "C:\\test\\TestProject.csproj",
                    "outputPath": "C:\\test\\obj"
                },
                "frameworks": {
                    "net8.0": {
                        "targetAlias": "net8.0",
                        "dependencies": {
                            "Newtonsoft.Json": { "target": "Package", "version": "[13.0.1, )" }
                        }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// Assets JSON with a PackageDownload dependency.
    /// </summary>
    private static string PackageDownloadAssetsJson() => """
        {
            "version": 3,
            "targets": { "net8.0": {} },
            "libraries": {},
            "projectFileDependencyGroups": { "net8.0": [] },
            "packageFolders": { "C:\\Users\\test\\.nuget\\packages\\": {} },
            "project": {
                "version": "1.0.0",
                "restore": {
                    "projectName": "TestProject",
                    "projectPath": "C:\\test\\TestProject.csproj",
                    "outputPath": "C:\\test\\obj"
                },
                "frameworks": {
                    "net8.0": {
                        "targetAlias": "net8.0",
                        "dependencies": {},
                        "downloadDependencies": [
                            { "name": "Microsoft.Net.Compilers.Toolset", "version": "[4.8.0, 4.8.0]" }
                        ]
                    }
                }
            }
        }
        """;

    /// <summary>
    /// Assets JSON with two target frameworks (net8.0 and net6.0), each containing the same package.
    /// </summary>
    private static string MultiTargetAssetsJson() => """
        {
            "version": 3,
            "targets": {
                "net8.0": {
                    "Newtonsoft.Json/13.0.1": {
                        "type": "package",
                        "compile": { "lib/net8.0/Newtonsoft.Json.dll": {} },
                        "runtime": { "lib/net8.0/Newtonsoft.Json.dll": {} }
                    }
                },
                "net6.0": {
                    "Newtonsoft.Json/13.0.1": {
                        "type": "package",
                        "compile": { "lib/net6.0/Newtonsoft.Json.dll": {} },
                        "runtime": { "lib/net6.0/Newtonsoft.Json.dll": {} }
                    }
                }
            },
            "libraries": {
                "Newtonsoft.Json/13.0.1": {
                    "sha512": "fakehash", "type": "package",
                    "path": "newtonsoft.json/13.0.1",
                    "files": [ "lib/net8.0/Newtonsoft.Json.dll", "lib/net6.0/Newtonsoft.Json.dll" ]
                }
            },
            "projectFileDependencyGroups": {
                "net8.0": [ "Newtonsoft.Json >= 13.0.1" ],
                "net6.0": [ "Newtonsoft.Json >= 13.0.1" ]
            },
            "packageFolders": { "C:\\Users\\test\\.nuget\\packages\\": {} },
            "project": {
                "version": "1.0.0",
                "restore": {
                    "projectName": "TestProject",
                    "projectPath": "C:\\test\\TestProject.csproj",
                    "outputPath": "C:\\test\\obj"
                },
                "frameworks": {
                    "net8.0": {
                        "targetAlias": "net8.0",
                        "dependencies": {
                            "Newtonsoft.Json": { "target": "Package", "version": "[13.0.1, )" }
                        }
                    },
                    "net6.0": {
                        "targetAlias": "net6.0",
                        "dependencies": {
                            "Newtonsoft.Json": { "target": "Package", "version": "[13.0.1, )" }
                        }
                    }
                }
            }
        }
        """;
}

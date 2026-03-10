#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using global::NuGet.Frameworks;
using global::NuGet.LibraryModel;
using global::NuGet.ProjectModel;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.DotNet;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DotNetComponentDetectorTests
{
    private static readonly string RootDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:" : @"/";

    /// <summary>
    /// The short NuGet TFM (e.g. "net8.0") that the test assembly itself targets.
    /// Used by real-restore tests so they don't break when the SDK drops an older framework.
    /// </summary>
    private static readonly string CurrentTfm = NuGetFramework.Parse(
        Assembly.GetExecutingAssembly().GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>().FrameworkName)
        .GetShortFolderName();

    private readonly DetectorTestUtilityBuilder<DotNetComponentDetector> detectorTestUtility = new();

    private readonly Mock<ILogger<DotNetComponentDetector>> mockLogger = new();

    // uses ExecuteCommandAsync
    private readonly Mock<ICommandLineInvocationService> mockCommandLineInvocationService = new();
    private readonly CommandLineExecutionResult commandLineExecutionResult = new();

    private readonly ICommandLineInvocationService realCommandLineService = new CommandLineInvocationService();

    // uses Exists, EnumerateFiles
    private readonly Mock<IDirectoryUtilityService> mockDirectoryUtilityService = new();

    // uses Exists, MakeFileStream
    private readonly Mock<IFileUtilityService> mockFileUtilityService = new();
    private readonly Dictionary<string, Dictionary<string, Stream>> files = [];

    // uses GetParentDirectory, NormalizePath
    private readonly Mock<IPathUtilityService> mockPathUtilityService = new();

    private Func<string, DirectoryInfo, CommandLineExecutionResult> commandLineCallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetComponentDetectorTests"/> class.
    /// </summary>
    public DotNetComponentDetectorTests()
    {
        this.detectorTestUtility.AddServiceMock(this.mockLogger)
                                .AddServiceMock(this.mockCommandLineInvocationService)
                                .AddServiceMock(this.mockDirectoryUtilityService)
                                .AddServiceMock(this.mockFileUtilityService)
                                .AddServiceMock(this.mockPathUtilityService);

        this.mockFileUtilityService.Setup(x => x.Exists(It.IsAny<string>())).Returns((string p) => this.FileExists(p));
        this.mockFileUtilityService.Setup(x => x.MakeFileStream(It.IsAny<string>())).Returns((string p) => this.OpenFile(p));
        this.mockDirectoryUtilityService.Setup(x => x.Exists(It.IsAny<string>())).Returns((string p) => this.DirectoryExists(p));

        // ignore pattern and search option since we don't really need them for tests
        this.mockDirectoryUtilityService.Setup(x => x.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns((string d, string p, SearchOption s) => this.EnumerateFilesRecursive(d, p));

        this.mockPathUtilityService.Setup(x => x.NormalizePath(It.IsAny<string>())).Returns((string p) => p);  // don't do normalization
        this.mockPathUtilityService.Setup(x => x.GetParentDirectory(It.IsAny<string>())).Returns((string p) => Path.GetDirectoryName(p));

        this.mockCommandLineInvocationService.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DirectoryInfo>(), It.IsAny<CancellationToken>(), It.IsAny<string[]>()))
            .Returns((string c, IEnumerable<string> ac, DirectoryInfo d, CancellationToken ct, string[] args) => Task.FromResult(this.CommandResult(c, d)));
    }

    private bool FileExists(string path)
    {
        var fileName = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path);

        return this.files.TryGetValue(directory, out var fileNames) &&
               fileNames.TryGetValue(fileName, out _);
    }

    private Stream OpenFile(string path)
    {
        var fileName = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path);

        return this.files.TryGetValue(directory, out var fileNames) &&
               fileNames.TryGetValue(fileName, out var stream) ? stream : null;
    }

    private bool DirectoryExists(string directory) => this.files.ContainsKey(directory);

    private IEnumerable<string> EnumerateFilesRecursive(string directory, string pattern)
    {
        if (this.files.TryGetValue(directory, out var fileNames))
        {
            // a basic approximation of globbing
            var patternRegex = new Regex(pattern.Replace(".", "\\.").Replace("*", ".*"));

            foreach (var fileName in fileNames.Keys)
            {
                var filePath = Path.Combine(directory, fileName);

                if (fileName.EndsWith(Path.DirectorySeparatorChar))
                {
                    foreach (var subFile in this.EnumerateFilesRecursive(Path.TrimEndingDirectorySeparator(filePath), pattern))
                    {
                        yield return subFile;
                    }
                }
                else
                {
                    if (patternRegex.IsMatch(fileName))
                    {
                        yield return filePath;
                    }
                }
            }
        }
    }

    private void AddFile(string path, Stream content)
    {
        var fileName = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path);
        this.AddDirectory(directory);
        this.files[directory][fileName] = content;
    }

    private void AddDirectory(string path, string subDirectory = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (subDirectory is not null)
        {
            // use a trailing slash to indicate a sub directory in the files collection
            subDirectory += Path.DirectorySeparatorChar;
        }

        if (this.files.TryGetValue(path, out var directoryFiles))
        {
            if (subDirectory is not null)
            {
                directoryFiles.Add(subDirectory, null);
            }
        }
        else
        {
            this.files.Add(path, subDirectory is null ? [] : new() { { subDirectory, null } });
            this.AddDirectory(Path.GetDirectoryName(path), Path.GetFileName(path));
        }
    }

    private void SetCommandResult(int exitCode, string stdOut = null, string stdErr = null)
    {
        this.commandLineCallback = null;
        this.commandLineExecutionResult.ExitCode = exitCode;
        this.commandLineExecutionResult.StdOut = stdOut;
        this.commandLineExecutionResult.StdErr = stdErr;
    }

    private void SetCommandResult(Func<string, DirectoryInfo, CommandLineExecutionResult> callback)
    {
        this.commandLineCallback = callback;
    }

    private CommandLineExecutionResult CommandResult(string command, DirectoryInfo directory) =>
        (this.commandLineCallback != null) ? this.commandLineCallback(command, directory) : this.commandLineExecutionResult;

    [TestCleanup]
    public void ClearMocks()
    {
        this.files.Clear();
        this.SetCommandResult(-1);
    }

    private static string ProjectAssets(string projectName, string outputPath, string projectPath, params string[] targetFrameworks)
    {
        return ProjectAssetsWithSelfContained(projectName, outputPath, projectPath, selfContainedTargetFrameworks: null, aotTargetFrameworks: null, targetFrameworks);
    }

    /// <summary>
    /// Creates a project assets JSON string for testing, with optional self-contained configuration.
    /// </summary>
    /// <param name="projectName">Name of the project.</param>
    /// <param name="outputPath">Output path for the project.</param>
    /// <param name="projectPath">Path to the project file.</param>
    /// <param name="selfContainedTargetFrameworks">Set of target frameworks that should be configured as self-contained (via runtime download dependencies). If null, none are self-contained.</param>
    /// <param name="aotTargetFrameworks">Set of target frameworks that should include a Microsoft.DotNet.ILCompiler reference (AOT). If null, none are AOT.</param>
    /// <param name="targetFrameworks">Target frameworks for the project.</param>
    private static string ProjectAssetsWithSelfContained(string projectName, string outputPath, string projectPath, ISet<string> selfContainedTargetFrameworks, ISet<string> aotTargetFrameworks = null, params string[] targetFrameworks)
    {
        LockFileFormat format = new();
        LockFile lockFile = new();
        using var textWriter = new StringWriter();

        // assets file always includes a trailing separator
        if (!Path.EndsInDirectorySeparator(outputPath))
        {
            outputPath += Path.DirectorySeparatorChar;
        }

        var targets = new List<LockFileTarget>();
        foreach (var tfm in targetFrameworks)
        {
            var framework = NuGetFramework.Parse(tfm);
            var isSelfContained = selfContainedTargetFrameworks != null && selfContainedTargetFrameworks.Contains(tfm);
            var isAot = aotTargetFrameworks != null && aotTargetFrameworks.Contains(tfm);

            var target = new LockFileTarget { TargetFramework = framework };

            // AOT projects have a Microsoft.DotNet.ILCompiler library in targets
            if (isAot)
            {
                target.Libraries.Add(new LockFileTargetLibrary { Name = "Microsoft.DotNet.ILCompiler", Version = new NuGetVersion("8.0.0"), Type = "package" });
            }

            targets.Add(target);

            // Self-contained projects have an additional RID-qualified target in their assets file
            if (isSelfContained)
            {
                targets.Add(new LockFileTarget { TargetFramework = framework, RuntimeIdentifier = "win-x64" });
            }
        }

        lockFile.Targets = targets;
        lockFile.PackageSpec = new()
        {
            RestoreMetadata = new()
            {
                ProjectName = projectName,
                OutputPath = outputPath,
                ProjectPath = projectPath,
            },
        };

        foreach (var tfm in targetFrameworks)
        {
            var isSelfContained = selfContainedTargetFrameworks != null && selfContainedTargetFrameworks.Contains(tfm);

            var tfi = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse(tfm),
                FrameworkReferences =
                [
                    new FrameworkDependency("Microsoft.NETCore.App", FrameworkDependencyFlags.All),
                ],
                DownloadDependencies = isSelfContained
                    ? [new DownloadDependency("Microsoft.NETCore.App.Ref", new VersionRange(new NuGetVersion("8.0.0"))), new DownloadDependency("Microsoft.NETCore.App.Runtime.win-x64", new VersionRange(new NuGetVersion("8.0.0")))]
                    : [],
            };

            lockFile.PackageSpec.TargetFrameworks.Add(tfi);
        }

        format.Write(textWriter, lockFile);
        return textWriter.ToString();
    }

    private static Stream GlobalJson(string sdkVersion)
    {
        var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("sdk");
            writer.WriteStartObject();
            writer.WriteString("version", sdkVersion);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        stream.Position = 0;
        return stream;
    }

    private static Stream StreamFromString(string content)
    {
        var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
            stream.Position = 0;
        }

        return stream;
    }

    /// <summary>
    /// Writes a .csproj (with isolation files so the repo's build config doesn't interfere),
    /// runs <c>dotnet restore</c>, and returns the path to the generated <c>project.assets.json</c>.
    /// The caller is responsible for cleaning up <paramref name="projectDir"/> if desired.
    /// </summary>
    private async Task<string> RestoreProjectAndGetAssetsPathAsync(string projectDir, string csproj)
    {
        Directory.CreateDirectory(projectDir);

        // Isolation files so the test project is not affected by the repo's
        // Directory.Build.props / .targets / Directory.Packages.props / global.json.
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Directory.Build.props"), "<Project/>");
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Directory.Build.targets"), "<Project/>");
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Directory.Packages.props"), "<Project/>");
        await File.WriteAllTextAsync(Path.Combine(projectDir, "global.json"), "{}");

        // Minimal source file so restore doesn't complain.
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Program.cs"), "return;");

        // The project definition supplied by the test.
        var csprojPath = Path.Combine(projectDir, "test.csproj");
        await File.WriteAllTextAsync(csprojPath, csproj);

        var result = await this.realCommandLineService.ExecuteCommandAsync(
            "dotnet",
            default,
            new DirectoryInfo(projectDir),
            $"restore \"{csprojPath}\"");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet restore failed (exit {result.ExitCode}).\nstdout:\n{result.StdOut}\nstderr:\n{result.StdErr}");
        }

        var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
        {
            throw new FileNotFoundException("project.assets.json was not generated by dotnet restore.", assetsPath);
        }

        return assetsPath;
    }

    [TestMethod]
    public async Task TestDotNetDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.detectorTestUtility.ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJson_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        var globalJson = GlobalJson("42.0.800");
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(-1);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "42.0.800 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "42.0.800 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJsonWithComments_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");

        var globalJson = StreamFromString("""
        // comment
        {
            // comment
            "sdk": {
                // comment
                "version": "8.2.100"
            }
        }
        """);
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(-1);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "8.2.100 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "8.2.100 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJsonWithTrailingCommas_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");

        // Trailing commas after version property and after sdk object
        var globalJson = StreamFromString("""
        {
            "sdk": {
                "version": "9.9.900",
            },
        }
        """);
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(-1); // force reading from file instead of dotnet --version

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "9.9.900 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "9.9.900 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJsonWithoutVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        var globalJson = StreamFromString("""
        {
            "msbuild-sdks": {
                "Microsoft.Build.NoTargets": "3.5.0",
                "Microsoft.DotNet.Arcade.Sdk": "10.0.0-beta.25206.1"
            }
        }
        """);
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(-1);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "unknown net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJsonRollForward_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        var globalJson = GlobalJson("8.0.100");
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(0, "8.0.808");

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "8.0.808 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "8.0.808 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJsonDotNetVersionFails_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        var globalJson = GlobalJson("8.0.100");
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(-1);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "8.0.100 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "8.0.100 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorGlobalJsonDotNetVersionThrows_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        var globalJson = GlobalJson("8.0.100");
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult((c, d) => throw new InvalidOperationException());

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "8.0.100 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "8.0.100 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorNoGlobalJson_ReturnsDotNetVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        this.AddFile(projectPath, null);
        this.SetCommandResult(0, "86.75.309");

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "86.75.309 net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorNoGlobalJsonNoDotnet_ReturnsUnknownVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        this.AddFile(projectPath, null);
        this.SetCommandResult(-1);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "unknown net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorInvalidProjectName_ReturnsUnknownVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var outputPath = Path.Combine(projectPath, "obj");
        var projectAssets = ProjectAssets("/foo/bar/projectName", outputPath, projectPath, "net8.0");
        this.AddFile(projectPath, null);
        this.AddDirectory(outputPath);
        this.SetCommandResult(-1);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "unknown net8.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorMultipleTargetFrameworks()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0", "net6.0", "netstandard2.0");
        this.AddFile(projectPath, null);
        this.SetCommandResult(0, "1.2.3");

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "1.2.3 net8.0 unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "1.2.3 net6.0 unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "1.2.3 netstandard2.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorMultipleProjectsWithDifferentOutputTypeAndSdkVersion()
    {
        // dotnet from global.json will be 4.5.6
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult((c, d) => new CommandLineExecutionResult()
        {
            ExitCode = 0,
            StdOut = d.FullName == globalJsonDir ? "4.5.6" : "1.2.3",
        });

        // set up a library project - under global.json
        var libraryProjectName = "library";
        var libraryProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{libraryProjectName}.csproj");
        this.AddFile(libraryProjectPath, null);
        var libraryOutputPath = Path.Combine(Path.GetDirectoryName(libraryProjectPath), "obj");
        var libraryAssetsPath = Path.Combine(libraryOutputPath, "project.assets.json");
        var libraryAssets = ProjectAssets("library", libraryOutputPath, libraryProjectPath, "net8.0", "net6.0", "netstandard2.0");
        var libraryAssemblyStream = File.OpenRead(typeof(DotNetComponent).Assembly.Location);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net8.0", "library.dll"), libraryAssemblyStream);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net6.0", "library.dll"), libraryAssemblyStream);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "netstandard2.0", "library.dll"), libraryAssemblyStream);

        // set up an application - not under global.json
        var applicationProjectName = "application";
        var applicationProjectPath = Path.Combine(RootDir, "anotherPath", "to", "project", $"{applicationProjectName}.csproj");
        this.AddFile(applicationProjectPath, null);
        var applicationOutputPath = Path.Combine(Path.GetDirectoryName(applicationProjectPath), "obj");
        var applicationAssetsPath = Path.Combine(applicationOutputPath, "project.assets.json");
        var applicationAssets = ProjectAssets("application", applicationOutputPath, applicationProjectPath, "net8.0", "net4.8");
        var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net8.0", "application.dll"), applicationAssemblyStream);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net4.8", "application.exe"), applicationAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(libraryAssetsPath, libraryAssets)
            .WithFile(applicationAssetsPath, applicationAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(6);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 library - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net6.0 library - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 netstandard2.0 library - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "1.2.3 net8.0 application - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "1.2.3 net48 application - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorExe()
    {
        // dotnet from global.json will be 4.5.6
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult(0, "4.5.6");

        // set up an application - not under global.json
        var applicationProjectName = "application";
        var applicationProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{applicationProjectName}.csproj");
        this.AddFile(applicationProjectPath, null);
        var applicationOutputPath = Path.Combine(Path.GetDirectoryName(applicationProjectPath), "obj");
        var applicationAssetsPath = Path.Combine(applicationOutputPath, "project.assets.json");
        var applicationAssets = ProjectAssets("application", applicationOutputPath, applicationProjectPath, "net4.8");
        var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net4.8", "application.exe"), applicationAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(applicationAssetsPath, applicationAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net48 application - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorInvalidOutputAssembly()
    {
        // dotnet from global.json will be 4.5.6
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);
        this.SetCommandResult(1, "4.5.6");

        // set up a library project - under global.json
        var libraryProjectName = "library";
        var libraryProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{libraryProjectName}.csproj");
        this.AddFile(libraryProjectPath, null);
        var libraryOutputPath = Path.Combine(Path.GetDirectoryName(libraryProjectPath), "obj");
        var libraryAssetsPath = Path.Combine(libraryOutputPath, "project.assets.json");
        var libraryAssets = ProjectAssets("library", libraryOutputPath, libraryProjectPath, "net8.0", "net6.0", "netstandard2.0");

        // empty 8KB stream
        var libraryAssemblyStream = new MemoryStream() { Position = 8 * 1024 };
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net8.0", "library.dll"), libraryAssemblyStream);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net6.0", "library.dll"), libraryAssemblyStream);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "netstandard2.0", "library.dll"), libraryAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(libraryAssetsPath, libraryAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net6.0 unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 netstandard2.0 unknown - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorNoGlobalJsonSourceRoot()
    {
        // DetectorTestUtility runs under Path.GetTempPath()
        var scanRoot = Path.TrimEndingDirectorySeparator(Path.GetTempPath());

        var projectPath = Path.Combine(scanRoot, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        this.AddFile(projectPath, null);
        this.SetCommandResult((c, d) =>
        {
            d.FullName.Should().BeEquivalentTo(scanRoot);
            return new CommandLineExecutionResult()
            {
                ExitCode = 0,
                StdOut = "0.0.0",
            };
        });

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "0.0.0 net8.0 unknown - DotNet").Should().ContainSingle();
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    private static IEnumerable<object[]> AdditionalPathSegments { get; } =
#pragma warning restore SA1201 // Elements should appear in the correct order
    [
        [string.Empty],
        [$"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}"],
        [$"{Path.AltDirectorySeparatorChar}{Path.DirectorySeparatorChar}"],
        [$"{Path.AltDirectorySeparatorChar}{Path.AltDirectorySeparatorChar}"],
    ];

    [TestMethod]
    [DynamicData(nameof(AdditionalPathSegments))]
    public async Task TestDotNetDetectorRebasePaths(string additionalPathSegment)
    {
        // DetectorTestUtility runs under Path.GetTempPath()
        var scanRoot = Path.TrimEndingDirectorySeparator(Path.GetTempPath());

        // dotnet from global.json will be 4.5.6
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(scanRoot, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        // make sure we find global.json and read it
        this.SetCommandResult(-1);

        // set up a library project - under global.json
        var libraryProjectName = "library";

        var libraryProjectPath = Path.Combine(scanRoot, "path", "to", "project", $"{libraryProjectName}.csproj");
        var libraryBuildProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{libraryProjectName}.csproj");
        this.AddFile(libraryProjectPath, null);

        var libraryOutputPath = Path.Combine(Path.GetDirectoryName(libraryProjectPath), "obj");
        var libraryBuildOutputPath = Path.Combine(Path.GetDirectoryName(libraryBuildProjectPath), "obj") + additionalPathSegment;
        var libraryAssetsPath = Path.Combine(libraryOutputPath, "project.assets.json");

        // use "build" paths to simulate an Assets file that has a different root.  Here the build assets have RootDir, but the scanned filesystem has scanRoot.
        var libraryAssets = ProjectAssets("library", libraryBuildOutputPath, libraryBuildProjectPath, "net8.0", "net6.0", "netstandard2.0");
        var libraryAssemblyStream = File.OpenRead(typeof(DotNetComponent).Assembly.Location);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net8.0", "library.dll"), libraryAssemblyStream);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net6.0", "library.dll"), libraryAssemblyStream);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "netstandard2.0", "library.dll"), libraryAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(libraryAssetsPath, libraryAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(4);

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 unknown unknown - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 library - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net6.0 library - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 netstandard2.0 library - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorSelfContainedWithSelfContainedProperty()
    {
        // Emit a self-contained .csproj, restore it, and use the real project.assets.json.
        var projectDir = Path.Combine(Path.GetTempPath(), "cd-test-selfcontained-" + Guid.NewGuid().ToString("N"));
        try
        {
            var csproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{CurrentTfm}</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
                    <SelfContained>true</SelfContained>
                  </PropertyGroup>
                </Project>
                """;

            var assetsPath = await this.RestoreProjectAndGetAssetsPathAsync(projectDir, csproj);

            // Parse the restored assets file to extract paths the detector will use.
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(assetsPath);
            var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;

            // Trim trailing separator so mock filesystem paths are consistent.
            // The detector will fall back to projectAssetsDirectory (derived from the
            // stream location, which has no trailing sep) for EnumerateFiles.
            var outputPath = Path.TrimEndingDirectorySeparator(lockFile.PackageSpec.RestoreMetadata.OutputPath);

            // Verify the restored assets have a RID-qualified target (e.g. net8.0/win-x64).
            lockFile.Targets.Should().Contain(t => t.RuntimeIdentifier != null, "self-contained restore should produce a RID-qualified target");

            var globalJson = GlobalJson("4.5.6");
            this.AddFile(Path.Combine(Path.GetDirectoryName(projectDir), "global.json"), globalJson);
            this.SetCommandResult(0, "4.5.6");

            this.AddFile(projectPath, null);

            using (var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location))
            {
                var memoryStream = new MemoryStream();
                await applicationAssemblyStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                this.AddFile(Path.Combine(outputPath, "Release", CurrentTfm, "test.dll"), memoryStream);
            }

            var assetsContent = await File.ReadAllTextAsync(assetsPath);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithFile(assetsPath, assetsContent)
                .ExecuteDetectorAsync();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();

            // Both the plain TFM and RID-qualified targets map to the same framework.
            // The detector should report application-selfcontained for this framework.
            discoveredComponents.Where(component => component.Component.Id == $"4.5.6 {CurrentTfm} application-selfcontained - DotNet").Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task TestDotNetDetectorSelfContainedLibrary()
    {
        // A library can also be self-contained when it sets SelfContained + RuntimeIdentifier.
        var projectDir = Path.Combine(Path.GetTempPath(), "cd-test-selfcontained-lib-" + Guid.NewGuid().ToString("N"));
        try
        {
            var csproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{CurrentTfm}</TargetFramework>
                    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
                    <SelfContained>true</SelfContained>
                  </PropertyGroup>
                </Project>
                """;

            var assetsPath = await this.RestoreProjectAndGetAssetsPathAsync(projectDir, csproj);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(assetsPath);
            var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;
            var outputPath = Path.TrimEndingDirectorySeparator(lockFile.PackageSpec.RestoreMetadata.OutputPath);

            // Self-contained library should also have a RID-qualified target.
            lockFile.Targets.Should().Contain(t => t.RuntimeIdentifier != null, "self-contained restore should produce a RID-qualified target");

            var globalJson = GlobalJson("4.5.6");
            this.AddFile(Path.Combine(Path.GetDirectoryName(projectDir), "global.json"), globalJson);
            this.SetCommandResult(0, "4.5.6");

            this.AddFile(projectPath, null);

            var libraryAssemblyStream = File.OpenRead(typeof(DotNetComponent).Assembly.Location);
            this.AddFile(Path.Combine(outputPath, "Release", CurrentTfm, "test.dll"), libraryAssemblyStream);

            var assetsContent = await File.ReadAllTextAsync(assetsPath);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithFile(assetsPath, assetsContent)
                .ExecuteDetectorAsync();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
            discoveredComponents.Where(component => component.Component.Id == $"4.5.6 {CurrentTfm} library-selfcontained - DotNet").Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task TestDotNetDetectorSelfContainedWithPublishAot()
    {
        // PublishAot implies native AOT compilation (self-contained).
        // The detector recognises this via the Microsoft.DotNet.ILCompiler reference
        // that the SDK injects at restore time, regardless of RuntimeIdentifier.
        var projectDir = Path.Combine(Path.GetTempPath(), "cd-test-aot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var csproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{CurrentTfm}</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <PublishAot>true</PublishAot>
                  </PropertyGroup>
                </Project>
                """;

            var assetsPath = await this.RestoreProjectAndGetAssetsPathAsync(projectDir, csproj);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(assetsPath);
            var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;
            var outputPath = Path.TrimEndingDirectorySeparator(lockFile.PackageSpec.RestoreMetadata.OutputPath);

            // PublishAot projects should have Microsoft.DotNet.ILCompiler in targets.
            lockFile.Targets.Should().Contain(
                t => t.Libraries.Any(lib => lib.Name.Equals("Microsoft.DotNet.ILCompiler", StringComparison.OrdinalIgnoreCase)),
                "PublishAot should produce an ILCompiler reference in the targets");

            var globalJson = GlobalJson("4.5.6");
            this.AddFile(Path.Combine(Path.GetDirectoryName(projectDir), "global.json"), globalJson);
            this.SetCommandResult(0, "4.5.6");

            this.AddFile(projectPath, null);

            var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
            this.AddFile(Path.Combine(outputPath, "Release", CurrentTfm, "test.dll"), applicationAssemblyStream);

            var assetsContent = await File.ReadAllTextAsync(assetsPath);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithFile(assetsPath, assetsContent)
                .ExecuteDetectorAsync();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
            discoveredComponents.Where(component => component.Component.Id == $"4.5.6 {CurrentTfm} application-selfcontained - DotNet").Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task TestDotNetDetectorNotSelfContained()
    {
        // Framework-dependent app — no RuntimeIdentifier, no SelfContained.
        var projectDir = Path.Combine(Path.GetTempPath(), "cd-test-fdd-" + Guid.NewGuid().ToString("N"));
        try
        {
            var csproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{CurrentTfm}</TargetFramework>
                    <OutputType>Exe</OutputType>
                  </PropertyGroup>
                </Project>
                """;

            var assetsPath = await this.RestoreProjectAndGetAssetsPathAsync(projectDir, csproj);

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(assetsPath);
            var projectPath = lockFile.PackageSpec.RestoreMetadata.ProjectPath;
            var outputPath = Path.TrimEndingDirectorySeparator(lockFile.PackageSpec.RestoreMetadata.OutputPath);

            // Framework-dependent should NOT have RID-qualified targets.
            lockFile.Targets.Should().NotContain(
                t => t.RuntimeIdentifier != null,
                "framework-dependent restore should not produce RID-qualified targets");

            var globalJson = GlobalJson("4.5.6");
            this.AddFile(Path.Combine(Path.GetDirectoryName(projectDir), "global.json"), globalJson);
            this.SetCommandResult(0, "4.5.6");

            this.AddFile(projectPath, null);

            var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
            this.AddFile(Path.Combine(outputPath, "Release", CurrentTfm, "test.dll"), applicationAssemblyStream);

            var assetsContent = await File.ReadAllTextAsync(assetsPath);

            var (scanResult, componentRecorder) = await this.detectorTestUtility
                .WithFile(assetsPath, assetsContent)
                .ExecuteDetectorAsync();

            scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

            var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
            discoveredComponents.Where(component => component.Component.Id == $"4.5.6 {CurrentTfm} application - DotNet").Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task TestDotNetDetectorSyntheticSelfContainedApplication()
    {
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult(0, "4.5.6");

        var applicationProjectName = "application";
        var applicationProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{applicationProjectName}.csproj");
        this.AddFile(applicationProjectPath, null);
        var applicationOutputPath = Path.Combine(Path.GetDirectoryName(applicationProjectPath), "obj");
        var applicationAssetsPath = Path.Combine(applicationOutputPath, "project.assets.json");

        var applicationAssets = ProjectAssetsWithSelfContained("application", applicationOutputPath, applicationProjectPath, new HashSet<string> { "net8.0" }, aotTargetFrameworks: null, "net8.0");
        var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net8.0", "application.dll"), applicationAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(applicationAssetsPath, applicationAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 application-selfcontained - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorSyntheticSelfContainedLibrary()
    {
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult(0, "4.5.6");

        var libraryProjectName = "library";
        var libraryProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{libraryProjectName}.csproj");
        this.AddFile(libraryProjectPath, null);
        var libraryOutputPath = Path.Combine(Path.GetDirectoryName(libraryProjectPath), "obj");
        var libraryAssetsPath = Path.Combine(libraryOutputPath, "project.assets.json");

        var libraryAssets = ProjectAssetsWithSelfContained("library", libraryOutputPath, libraryProjectPath, new HashSet<string> { "net8.0" }, aotTargetFrameworks: null, "net8.0");
        var libraryAssemblyStream = File.OpenRead(typeof(DotNetComponent).Assembly.Location);
        this.AddFile(Path.Combine(libraryOutputPath, "Release", "net8.0", "library.dll"), libraryAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(libraryAssetsPath, libraryAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 library-selfcontained - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorSyntheticAotApplication()
    {
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult(0, "4.5.6");

        var applicationProjectName = "application";
        var applicationProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{applicationProjectName}.csproj");
        this.AddFile(applicationProjectPath, null);
        var applicationOutputPath = Path.Combine(Path.GetDirectoryName(applicationProjectPath), "obj");
        var applicationAssetsPath = Path.Combine(applicationOutputPath, "project.assets.json");

        // AOT: ILCompiler in targets, no framework reference + download dependency needed
        var applicationAssets = ProjectAssetsWithSelfContained("application", applicationOutputPath, applicationProjectPath, selfContainedTargetFrameworks: null, new HashSet<string> { "net8.0" }, "net8.0");
        var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net8.0", "application.dll"), applicationAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(applicationAssetsPath, applicationAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 application-selfcontained - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorSyntheticNotSelfContained()
    {
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult(0, "4.5.6");

        var applicationProjectName = "application";
        var applicationProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{applicationProjectName}.csproj");
        this.AddFile(applicationProjectPath, null);
        var applicationOutputPath = Path.Combine(Path.GetDirectoryName(applicationProjectPath), "obj");
        var applicationAssetsPath = Path.Combine(applicationOutputPath, "project.assets.json");

        // Framework-dependent: no self-contained, no AOT
        var applicationAssets = ProjectAssets("application", applicationOutputPath, applicationProjectPath, "net8.0");
        var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net8.0", "application.dll"), applicationAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(applicationAssetsPath, applicationAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 application - DotNet").Should().ContainSingle();
    }

    [TestMethod]
    public async Task TestDotNetDetectorMultiTargetWithMixedSelfContained()
    {
        // NuGet restore applies RuntimeIdentifier globally in cross-targeting, so a real
        // restore can't produce per-TFM mixed self-contained/framework-dependent targets.
        // Use the synthetic helper which correctly models per-TFM download dependencies
        // and RID-qualified targets.
        var globalJson = GlobalJson("4.5.6");
        var globalJsonDir = Path.Combine(RootDir, "path");
        this.AddFile(Path.Combine(globalJsonDir, "global.json"), globalJson);

        this.SetCommandResult(0, "4.5.6");

        var applicationProjectName = "application";
        var applicationProjectPath = Path.Combine(RootDir, "path", "to", "project", $"{applicationProjectName}.csproj");
        this.AddFile(applicationProjectPath, null);
        var applicationOutputPath = Path.Combine(Path.GetDirectoryName(applicationProjectPath), "obj");
        var applicationAssetsPath = Path.Combine(applicationOutputPath, "project.assets.json");

        // Multi-target: net8.0 is self-contained (has runtime downloads + RID target), net6.0 is not
        var applicationAssets = ProjectAssetsWithSelfContained("application", applicationOutputPath, applicationProjectPath, new HashSet<string> { "net8.0" }, aotTargetFrameworks: null, "net8.0", "net6.0");
        var applicationAssemblyStream = File.OpenRead(Assembly.GetEntryAssembly().Location);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net8.0", "application.dll"), applicationAssemblyStream);
        this.AddFile(Path.Combine(applicationOutputPath, "Release", "net6.0", "application.dll"), applicationAssemblyStream);

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(applicationAssetsPath, applicationAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var discoveredComponents = componentRecorder.GetDetectedComponents().ToArray();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net8.0 application-selfcontained - DotNet").Should().ContainSingle();
        discoveredComponents.Where(component => component.Component.Id == "4.5.6 net6.0 application - DotNet").Should().ContainSingle();
    }
}

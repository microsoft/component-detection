namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using global::NuGet.Frameworks;
using global::NuGet.ProjectModel;
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
public class DotNetComponentDetectorTests : BaseDetectorTest<DotNetComponentDetector>
{
    private static readonly string RootDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:" : @"\";

    private readonly Mock<ILogger<DotNetComponentDetector>> mockLogger = new();

    // uses ExecuteCommandAsync
    private readonly Mock<ICommandLineInvocationService> mockCommandLineInvocationService = new();
    private readonly CommandLineExecutionResult commandLineExecutionResult = new();

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
        this.DetectorTestUtility.AddServiceMock(this.mockLogger)
                                .AddServiceMock(this.mockCommandLineInvocationService)
                                .AddServiceMock(this.mockDirectoryUtilityService)
                                .AddServiceMock(this.mockFileUtilityService)
                                .AddServiceMock(this.mockPathUtilityService);

        this.mockFileUtilityService.Setup(x => x.Exists(It.IsAny<string>())).Returns((string p) => this.FileExists(p));
        this.mockFileUtilityService.Setup(x => x.MakeFileStream(It.IsAny<string>())).Returns((string p) => this.OpenFile(p));
        this.mockDirectoryUtilityService.Setup(x => x.Exists(It.IsAny<string>())).Returns((string p) => this.DirectoryExists(p));

        // ignore pattern and search option since we don't really need them for tests
        this.mockDirectoryUtilityService.Setup(x => x.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns((string d, string p, SearchOption s) => this.EnumerateFilesRecursive(d));

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

    private IEnumerable<string> EnumerateFilesRecursive(string directory)
    {
        if (this.files.TryGetValue(directory, out var fileNames))
        {
            foreach (var fileName in fileNames.Keys)
            {
                var filePath = Path.Combine(directory, fileName);

                if (fileName.EndsWith(Path.DirectorySeparatorChar))
                {
                    foreach (var subFile in this.EnumerateFilesRecursive(filePath.TrimEnd(Path.DirectorySeparatorChar)))
                    {
                        yield return subFile;
                    }
                }
                else
                {
                    yield return filePath;
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
        LockFileFormat format = new();
        LockFile lockFile = new();
        using var textWriter = new StringWriter();

        lockFile.Targets = targetFrameworks.Select(tfm => new LockFileTarget() { TargetFramework = NuGetFramework.Parse(tfm) }).ToList();
        lockFile.PackageSpec = new()
        {
            RestoreMetadata = new()
            {
                ProjectName = projectName,
                OutputPath = outputPath,
                ProjectPath = projectPath,
            },
        };

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

    [TestMethod]
    public async Task TestDotNetDetectorWithNoFiles_ReturnsSuccessfullyAsync()
    {
        var (scanResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

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

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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
    public async Task TestDotNetDetectorGlobalJsonRollForward_ReturnsSDKVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        var globalJson = GlobalJson("8.0.100");
        this.AddFile(projectPath, null);
        this.AddFile(Path.Combine(RootDir, "path", "global.json"), globalJson);
        this.SetCommandResult(0, "8.0.808");

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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
    public async Task TestDotNetDetectorNoGlobalJson_ReturnsDotNetVersion()
    {
        var projectPath = Path.Combine(RootDir, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        this.AddFile(projectPath, null);
        this.SetCommandResult(0, "86.75.309");

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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
            StdOut = d.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) == globalJsonDir ? "4.5.6" : "1.2.3",
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

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
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
        var scanRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var projectPath = Path.Combine(scanRoot, "path", "to", "project");
        var projectAssets = ProjectAssets("projectName", "does-not-exist", projectPath, "net8.0");
        this.AddFile(projectPath, null);
        this.SetCommandResult((c, d) =>
        {
            d.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Should().BeEquivalentTo(scanRoot);
            return new CommandLineExecutionResult()
            {
                ExitCode = 0,
                StdOut = "0.0.0",
            };
        });

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", projectAssets)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var discoveredComponents = detectedComponents.ToArray();
        discoveredComponents.Where(component => component.Component.Id == "0.0.0 net8.0 unknown - DotNet").Should().ContainSingle();
    }
}

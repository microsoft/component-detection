namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using FluentAssertions;

public static class MSBuildTestUtilities
{
    public const int TestTargetFrameworkVersion = 6;
    public static readonly string TestTargetFramework = $"net{TestTargetFrameworkVersion}.0";

    // we need to find the file `Microsoft.NETCoreSdk.BundledVersions.props` in the SDK directory
    private static readonly Lazy<string> BundledVersionsPropsPath = new(static () =>
    {
        // get the sdk version
        using var tempDir = new TemporaryProjectDirectory();
        var projectContents = @"
            <Project Sdk=""Microsoft.NET.Sdk"">
                <Target Name=""_ReportCurrentSdkVersion"">
                <Message Text=""_CurrentSdkVersion=$(NETCoreSdkVersion)"" Importance=""High"" />
                </Target>
            </Project>
            ";
        var projectPath = Path.Combine(tempDir.DirectoryPath, "project.csproj");
        File.WriteAllText(projectPath, projectContents);
        var (exitCode, stdout, stderr) = RunProcessAsync("dotnet", $"msbuild {projectPath} /t:_ReportCurrentSdkVersion").Result;
        if (exitCode != 0)
        {
            throw new NotSupportedException($"Failed to report the current SDK version:\n{stdout}\n{stderr}");
        }

        var matches = Regex.Matches(stdout, "_CurrentSdkVersion=(?<SdkVersion>.*)$", RegexOptions.Multiline);
        if (matches.Count == 0)
        {
            throw new NotSupportedException($"Failed to find the current SDK version in the output:\n{stdout}");
        }

        var sdkVersionString = matches.First().Groups["SdkVersion"].Value.Trim();

        // find the actual SDK directory
        var privateCoreLibPath = typeof(object).Assembly.Location; // e.g., C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.4\System.Private.CoreLib.dll
        var sdkDirectory = Path.Combine(Path.GetDirectoryName(privateCoreLibPath), "..", "..", "..", "sdk", sdkVersionString); // e.g., C:\Program Files\dotnet\sdk\8.0.204
        var bundledVersionsPropsPath = Path.Combine(sdkDirectory, "Microsoft.NETCoreSdk.BundledVersions.props");
        var normalizedPath = new FileInfo(bundledVersionsPropsPath);
        return normalizedPath.FullName;
    });

    public static async Task<Stream> GetBinLogStreamFromFileContentsAsync(
        string projectContents,
        (string FileName, string Contents)[] additionalFiles = null,
        (string Name, string Version, string TargetFramework, string AdditionalMetadataXml)[] mockedPackages = null)
    {
        // write all files
        using var tempDir = new TemporaryProjectDirectory();
        var fullProjectPath = Path.Combine(tempDir.DirectoryPath, "project.csproj");
        await File.WriteAllTextAsync(fullProjectPath, projectContents);
        if (additionalFiles is not null)
        {
            foreach (var (fileName, contents) in additionalFiles)
            {
                var fullFilePath = Path.Combine(tempDir.DirectoryPath, fileName);
                var fullFileDirectory = Path.GetDirectoryName(fullFilePath);
                Directory.CreateDirectory(fullFileDirectory);
                await File.WriteAllTextAsync(fullFilePath, contents);
            }
        }

        await MockNuGetPackagesInDirectoryAsync(tempDir, mockedPackages);

        // generate the binlog
        var (exitCode, stdOut, stdErr) = await RunProcessAsync("dotnet", $"build \"{fullProjectPath}\" /t:GenerateBuildDependencyFile /bl:msbuild.binlog", workingDirectory: tempDir.DirectoryPath);
        exitCode.Should().Be(0, $"STDOUT:\n{stdOut}\n\nSTDERR:\n{stdErr}");

        // copy it to memory so the temporary directory can be cleaned up
        var fullBinLogPath = Path.Combine(tempDir.DirectoryPath, "msbuild.binlog");
        using var binLogStream = File.OpenRead(fullBinLogPath);
        var inMemoryStream = new MemoryStream();
        await binLogStream.CopyToAsync(inMemoryStream);
        inMemoryStream.Position = 0;
        return inMemoryStream;
    }

    private static async Task MockNuGetPackagesInDirectoryAsync(
        TemporaryProjectDirectory tempDir,
        (string Name, string Version, string TargetFramework, string AdditionalMetadataXml)[] mockedPackages)
    {
        if (mockedPackages is not null)
        {
            var nugetConfig = @"
                <configuration>
                  <packageSources>
                    <clear />
                    <add key=""local-feed"" value=""local-packages"" />
                  </packageSources>
                </configuration>
                ";
            await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "NuGet.Config"), nugetConfig);
            var packagesPath = Path.Combine(tempDir.DirectoryPath, "local-packages");
            Directory.CreateDirectory(packagesPath);

            var mockedPackagesWithFiles = mockedPackages.Select(p =>
            {
                return (
                    p.Name,
                    p.Version,
                    p.TargetFramework,
                    p.AdditionalMetadataXml,
                    Files: new[] { ($"lib/{p.TargetFramework}/{p.Name}.dll", Array.Empty<byte>()) });
            });

            var allPackages = mockedPackagesWithFiles.Concat(GetCommonPackages());

            using var sha512 = SHA512.Create(); // this is used to compute the hash of each package below
            foreach (var package in allPackages)
            {
                var nuspec = NugetTestUtilities.GetValidNuspec(package.Name, package.Version, Array.Empty<string>());
                if (package.AdditionalMetadataXml is not null)
                {
                    // augment the nuspec
                    var doc = XDocument.Parse(nuspec);
                    var additionalMetadata = XElement.Parse(package.AdditionalMetadataXml);
                    additionalMetadata = WithNamespace(additionalMetadata, doc.Root.Name.Namespace);

                    var metadataElement = doc.Root.Descendants().First(e => e.Name.LocalName == "metadata");
                    metadataElement.Add(additionalMetadata);
                    nuspec = doc.ToString();
                }

                var nupkg = await NugetTestUtilities.ZipNupkgComponentAsync(package.Name, nuspec, additionalFiles: package.Files);

                // to create a local nuget package source, we need a directory structure like this:
                // local-packages/<package-name>/<package-version>/
                var packagePath = Path.Combine(packagesPath, package.Name.ToLower(), package.Version.ToLower());
                Directory.CreateDirectory(packagePath);

                // and we need the following files:
                // 1. the package
                var nupkgPath = Path.Combine(packagePath, $"{package.Name}.{package.Version}.nupkg".ToLower());
                using (var nupkgFileStream = File.OpenWrite(nupkgPath))
                {
                    await nupkg.CopyToAsync(nupkgFileStream);
                }

                // 2. the nuspec
                var nuspecPath = Path.Combine(packagePath, $"{package.Name}.nuspec".ToLower());
                await File.WriteAllTextAsync(nuspecPath, nuspec);

                // 3. SHA512 hash of the package
                var hash = sha512.ComputeHash(File.ReadAllBytes(nupkgPath));
                var hashString = Convert.ToBase64String(hash);
                var hashPath = $"{nupkgPath}.sha512";
                await File.WriteAllTextAsync(hashPath, hashString);

                // 4. a JSON metadata file
                var metadata = $@"{{""version"": 2, ""contentHash"": ""{hashString}"", ""source"": null}}";
                var metadataPath = Path.Combine(packagePath, ".nupkg.metadata");
                await File.WriteAllTextAsync(metadataPath, metadata);
            }
        }
    }

    private static XElement WithNamespace(XElement element, XNamespace ns)
    {
        return new XElement(
            ns + element.Name.LocalName,
            element.Attributes(),
            element.Elements().Select(e => WithNamespace(e, ns)),
            element.Value);
    }

    private static IEnumerable<(string Name, string Version, string TargetFramework, string AdditionalMetadataXml, (string Path, byte[] Content)[] Files)> GetCommonPackages()
    {
        // to allow the tests to not require the network, we need to mock some common packages
        yield return MakeWellKnownReferencePackage("Microsoft.AspNetCore.App", null);
        yield return MakeWellKnownReferencePackage("Microsoft.WindowsDesktop.App", null);

        var frameworksXml = $@"
            <FileList TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""{TestTargetFrameworkVersion}.0"" FrameworkName=""Microsoft.NETCore.App"" Name="".NET Runtime"">
            </FileList>
            ";
        yield return MakeWellKnownReferencePackage("Microsoft.NETCore.App", new[] { ("data/FrameworkList.xml", Encoding.UTF8.GetBytes(frameworksXml)) });
    }

    private static (string Name, string Version, string TargetFramework, string AdditionalMetadataXml, (string Path, byte[] Content)[] Files) MakeWellKnownReferencePackage(string packageName, (string Path, byte[] Content)[] files)
    {
        var propsDocument = XDocument.Load(BundledVersionsPropsPath.Value);
        var xpathQuery = $@"
            /Project/ItemGroup/KnownFrameworkReference
                [
                    @Include='{packageName}' and
                    @TargetingPackName='{packageName}.Ref' and
                    @TargetFramework='{TestTargetFramework}'
                ]
            ";
        var matchingFrameworkElement = propsDocument.XPathSelectElement(xpathQuery);
        if (matchingFrameworkElement is null)
        {
            throw new NotSupportedException($"Unable to find {packageName}.Ref");
        }

        var expectedVersion = matchingFrameworkElement.Attribute("TargetingPackVersion").Value;
        return (
            $"{packageName}.Ref",
            expectedVersion,
            TestTargetFramework,
            "<packageTypes><packageType name=\"DotnetPlatform\" /></packageTypes>",
            files);
    }

    public static Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string fileName, string arguments = "", string workingDirectory = null)
    {
        var tcs = new TaskCompletionSource<(int, string, string)>();

        var redirectInitiated = new ManualResetEventSlim();
        var process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false, // required to redirect output
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };

        if (workingDirectory is not null)
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => stdout.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);

        process.Exited += (sender, args) =>
        {
            // It is necessary to wait until we have invoked 'BeginXReadLine' for our redirected IO. Then,
            // we must call WaitForExit to make sure we've received all OutputDataReceived/ErrorDataReceived calls
            // or else we'll be returning a list we're still modifying. For paranoia, we'll start a task here rather
            // than enter right back into the Process type and start a wait which isn't guaranteed to be safe.
            var unused = Task.Run(() =>
            {
                redirectInitiated.Wait();
                redirectInitiated.Dispose();
                redirectInitiated = null;

                process.WaitForExit();

                tcs.TrySetResult((process.ExitCode, stdout.ToString(), stderr.ToString()));
                process.Dispose();
            });
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Process failed to start");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        redirectInitiated.Set();

        return tcs.Task;
    }

    private class TemporaryProjectDirectory : IDisposable
    {
        private const string DirectoryBuildPropsContents = @"
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            ";

        private readonly Dictionary<string, string> originalEnvironment = new();

        public TemporaryProjectDirectory()
        {
            var testDataPath = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "test-data");
            Directory.CreateDirectory(testDataPath);

            // ensure tests don't crawl the directory tree
            File.WriteAllText(Path.Combine(testDataPath, "Directory.Build.props"), DirectoryBuildPropsContents);
            File.WriteAllText(Path.Combine(testDataPath, "Directory.Build.targets"), "<Project />");
            File.WriteAllText(Path.Combine(testDataPath, "Directory.Packages.props"), "<Project />");

            // create temporary project directory
            this.DirectoryPath = Path.Combine(testDataPath, Guid.NewGuid().ToString("d"));
            Directory.CreateDirectory(this.DirectoryPath);

            // ensure each project gets a fresh package cache
            foreach (var envName in new[] { "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_SCRATCH", "NUGET_PLUGINS_CACHE_PATH" })
            {
                this.originalEnvironment[envName] = Environment.GetEnvironmentVariable(envName);
                var dir = Path.Join(this.DirectoryPath, envName);
                Directory.CreateDirectory(dir);
                Environment.SetEnvironmentVariable(envName, dir);
            }
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            foreach (var (key, value) in this.originalEnvironment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            try
            {
                Directory.Delete(this.DirectoryPath, recursive: true);
            }
            catch
            {
            }
        }
    }
}

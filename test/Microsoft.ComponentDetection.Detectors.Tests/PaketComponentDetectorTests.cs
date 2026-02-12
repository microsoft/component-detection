namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Paket;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PaketComponentDetectorTests : BaseDetectorTest<PaketComponentDetector>
{
    [TestMethod]
    public async Task TestPaketDetector_SimpleNuGetPackages()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    Castle.Core (3.3.0)
    log4net (1.2.10)
    Castle.Core-log4net (3.3.0)
      Castle.Core (>= 3.3.0)
      log4net (>= 1.2.10)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(3);

        var castleCore = detectedComponents.Single(c => c.Component.Id.Contains("Castle.Core 3.3.0"));
        castleCore.Should().NotBeNull();

        var log4net = detectedComponents.Single(c => c.Component.Id.Contains("log4net 1.2.10"));
        log4net.Should().NotBeNull();

        var castleCoreLog4Net = detectedComponents.Single(c => c.Component.Id.Contains("Castle.Core-log4net 3.3.0"));
        castleCoreLog4Net.Should().NotBeNull();
    }

    [TestMethod]
    public async Task TestPaketDetector_WithDependencies()
    {
        var paketLock = @"NUGET
  remote: https://nuget.org/api/v2
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Rx-Main (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
      Rx-Linq (>= 2.2.5)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(2);

        var castleWindsor = detectedComponents.Single(c => c.Component.Id.Contains("Castle.Windsor 3.3.0"));
        castleWindsor.Should().NotBeNull();

        var rxMain = detectedComponents.Single(c => c.Component.Id.Contains("Rx-Main 2.2.5"));
        rxMain.Should().NotBeNull();
    }

    [TestMethod]
    public async Task TestPaketDetector_ComplexLockFile()
    {
        var paketLock = @"NUGET
  remote: https://nuget.org/api/v2
    Castle.Core (3.3.0)
    Castle.Core-log4net (3.3.0)
      Castle.Core (>= 3.3.0)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    Rx-Core (2.2.5)
      Rx-Interfaces (>= 2.2.5)
    Rx-Interfaces (2.2.5)
    Rx-Linq (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
    Rx-Main (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
      Rx-Linq (>= 2.2.5)
      Rx-PlatformServices (>= 2.2.5)
    Rx-PlatformServices (2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Core (>= 2.2.5)
    log4net (1.2.10)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(11);

        // Verify some key packages
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Windsor 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Main 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("log4net 1.2.10"));
    }

    [TestMethod]
    public async Task TestPaketDetector_EmptyFile()
    {
        var paketLock = string.Empty;

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPaketDetector_OnlyNuGetSection()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    Newtonsoft.Json (13.0.1)

GITHUB
  remote: owner/repo
    src/File.fs (abc123)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should only detect the NuGet package, not the GitHub dependency
        detectedComponents.Should().ContainSingle(c => c.Component.Id.Contains("Newtonsoft.Json 13.0.1"));
    }

    [TestMethod]
    public async Task TestPaketDetector_MultipleRemoteSources()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    Newtonsoft.Json (13.0.1)
  remote: https://www.myget.org/F/myfeed/api/v3/index.json
    MyPackage (1.0.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(2);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Newtonsoft.Json 13.0.1"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("MyPackage 1.0.0"));
    }

    [TestMethod]
    public async Task TestPaketDetector_VersionWithBuildMetadata()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    MyPackage (1.0.0-beta.1)
    AnotherPackage (2.3.4+build.5678)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(2);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("MyPackage 1.0.0-beta.1"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("AnotherPackage 2.3.4"));
    }

    [TestMethod]
    public async Task TestPaketDetector_DependenciesWithDifferentVersionConstraints()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    PackageA (1.0.0)
      PackageB (>= 2.0.0)
      PackageC (< 3.0.0)
      PackageD (~> 1.5)
      PackageE (1.2.3)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(1);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("PackageA 1.0.0"));
    }

    [TestMethod]
    public async Task TestPaketDetector_PackageWithNoVersion()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    InvalidPackage
    ValidPackage (1.0.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should only detect the valid package
        detectedComponents.Should().ContainSingle(c => c.Component.Id.Contains("ValidPackage 1.0.0"));
    }

    [TestMethod]
    public async Task TestPaketDetector_RealWorldExample()
    {
        var paketLock = @"RESTRICTION: == net8.0
NUGET
  remote: https://api.nuget.org/v3/index.json
    FSharp.Core (8.0.200)
    Microsoft.Extensions.Logging.Abstractions (8.0.1)
      Microsoft.Extensions.DependencyInjection.Abstractions (>= 8.0.1)
    Microsoft.Extensions.DependencyInjection.Abstractions (8.0.1)
    Newtonsoft.Json (13.0.3)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(4);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core 8.0.200"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Microsoft.Extensions.Logging.Abstractions 8.0.1"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Microsoft.Extensions.DependencyInjection.Abstractions 8.0.1"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Newtonsoft.Json 13.0.3"));
    }

    [TestMethod]
    public async Task TestPaketDetector_WithMultipleGroups()
    {
        var paketLock = @"GROUP Build
RESTRICTION: == net6.0
NUGET
  remote: https://api.nuget.org/v3/index.json
    FSharp.Core (9.0.300)
    Newtonsoft.Json (13.0.3)

GROUP Server
STORAGE: NONE
NUGET
  remote: https://api.nuget.org/v3/index.json
    Azure.Core (1.46.1)
    FSharp.Core (9.0.303)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should detect packages from both groups
        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(3);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Newtonsoft.Json 13.0.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Azure.Core 1.46.1"));
    }

    [TestMethod]
    public async Task TestPaketDetector_WithDependencyRestrictions()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    Azure.Core (1.46.1) - restriction: || (&& (>= net462) (>= netstandard2.0)) (>= net8.0)
      Microsoft.Bcl.AsyncInterfaces (>= 8.0) - restriction: || (>= net462) (>= netstandard2.0)
      System.Memory.Data (>= 6.0.1) - restriction: || (>= net462) (>= netstandard2.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should detect the main package and its dependencies despite restrictions
        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(1);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Azure.Core 1.46.1"));
    }

    [TestMethod]
    public async Task TestPaketDetector_IgnoresHttpAndGitHubSections()
    {
        var paketLock = @"GROUP Clientside
GITHUB
  remote: zurb/bower-foundation
    css/foundation.css (15d98294916c50ce8e6838bc035f4f136d4dc704)
    js/foundation.min.js (15d98294916c50ce8e6838bc035f4f136d4dc704)
HTTP
  remote: https://cdn.jsdelivr.net
    jquery.signalR.js (/npm/signalr@2.4.3/jquery.signalR.js)
    lodash.min.js (/npm/lodash@4.17.21/lodash.min.js)
NUGET
  remote: https://api.nuget.org/v3/index.json
    Newtonsoft.Json (13.0.3)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should only detect NuGet packages, not GITHUB or HTTP dependencies
        detectedComponents.Should().ContainSingle(c => c.Component.Id.Contains("Newtonsoft.Json 13.0.3"));
    }

    [TestMethod]
    public async Task TestPaketDetector_WithStorageDirective()
    {
        var paketLock = @"GROUP Server
STORAGE: NONE
NUGET
  remote: https://api.nuget.org/v3/index.json
    FSharp.Core (9.0.303)
    Oxpecker (1.3)
      FSharp.Core (>= 9.0.201) - restriction: >= net8.0
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should detect packages regardless of STORAGE directive
        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(2);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core 9.0.303"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Oxpecker 1.3"));
    }

    [TestMethod]
    public async Task TestPaketDetector_ComplexRealWorldFile()
    {
        var paketLock = @"GROUP Build
RESTRICTION: == net6.0
NUGET
  remote: https://api.nuget.org/v3/index.json
    Fake.Core.Target (6.1.3)
      Fake.Core.CommandLineParsing (>= 6.1.3)
      Fake.Core.Context (>= 6.1.3)
      FSharp.Core (>= 8.0.301)
    FSharp.Core (9.0.300)

GROUP Server
STORAGE: NONE
NUGET
  remote: https://api.nuget.org/v3/index.json
    FSharp.Data (6.6)
      FSharp.Core (>= 6.0.1) - restriction: >= netstandard2.0
      FSharp.Data.Csv.Core (>= 6.6) - restriction: >= netstandard2.0
    Microsoft.AspNetCore.SignalR (1.2)
      Microsoft.AspNetCore.Http.Connections (>= 1.2) - restriction: >= netstandard2.0
    Serilog (4.2) - restriction: || (>= net462) (>= netstandard2.0)
      System.Diagnostics.DiagnosticSource (>= 8.0.1) - restriction: || (&& (>= net462) (< netstandard2.0)) (&& (< net462) (< net6.0) (>= netstandard2.0)) (>= net471)

GROUP Test
NUGET
  remote: https://api.nuget.org/v3/index.json
    NUnit (4.3.2)
      System.Memory (>= 4.6) - restriction: >= net462
    NUnit3TestAdapter (5.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should detect packages from all groups with various restriction formats
        detectedComponents.Should().HaveCountGreaterThanOrEqualTo(6);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Fake.Core.Target 6.1.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Data 6.6"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Microsoft.AspNetCore.SignalR 1.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Serilog 4.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("NUnit 4.3.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("NUnit3TestAdapter 5.0"));
    }
}

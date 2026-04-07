namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Paket;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
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

        // Only 3 resolved packages (4-space lines), not 5 (which would include 6-space dependency specs)
        detectedComponents.Should().HaveCount(3);

        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("log4net 1.2.10"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core-log4net 3.3.0"));
    }

    [TestMethod]
    public async Task TestPaketDetector_DependencyRelationshipsAreBuilt()
    {
        var paketLock = @"NUGET
  remote: https://nuget.org/api/v2
    Castle.Core (3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Only 2 resolved packages
        detectedComponents.Should().HaveCount(2);

        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Windsor 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core 3.3.0"));

        // Validate dependency graph
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        // Castle.Windsor is a root (not a dependency of anything)
        dependencyGraph.IsComponentExplicitlyReferenced("Castle.Windsor 3.3.0 - NuGet").Should().BeTrue();

        // Castle.Core is a dependency of Castle.Windsor, so it's transitive
        dependencyGraph.IsComponentExplicitlyReferenced("Castle.Core 3.3.0 - NuGet").Should().BeFalse();

        // Castle.Windsor depends on Castle.Core
        dependencyGraph.GetDependenciesForComponent("Castle.Windsor 3.3.0 - NuGet")
            .Should().Contain("Castle.Core 3.3.0 - NuGet");

        // Castle.Core is a leaf
        dependencyGraph.GetDependenciesForComponent("Castle.Core 3.3.0 - NuGet")
            .Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPaketDetector_WithDependencies()
    {
        var paketLock = @"NUGET
  remote: https://nuget.org/api/v2
    Castle.Core (3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
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
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // 6 resolved packages
        detectedComponents.Should().HaveCount(6);

        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Windsor 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Main 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Core 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Interfaces 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Linq 2.2.5"));

        // Validate dependency graph edges
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        // Rx-Main depends on Rx-Interfaces, Rx-Core, and Rx-Linq
        dependencyGraph.GetDependenciesForComponent("Rx-Main 2.2.5 - NuGet")
            .Should().BeEquivalentTo(["Rx-Interfaces 2.2.5 - NuGet", "Rx-Core 2.2.5 - NuGet", "Rx-Linq 2.2.5 - NuGet"]);

        // Rx-Core depends on Rx-Interfaces
        dependencyGraph.GetDependenciesForComponent("Rx-Core 2.2.5 - NuGet")
            .Should().BeEquivalentTo(["Rx-Interfaces 2.2.5 - NuGet"]);

        // Castle.Windsor depends on Castle.Core
        dependencyGraph.GetDependenciesForComponent("Castle.Windsor 3.3.0 - NuGet")
            .Should().BeEquivalentTo(["Castle.Core 3.3.0 - NuGet"]);

        // Explicit roots: Castle.Windsor and Rx-Main (not depended on by anything)
        var explicitRoots = dependencyGraph.GetAllExplicitlyReferencedComponents();
        explicitRoots.Should().Contain("Castle.Windsor 3.3.0 - NuGet");
        explicitRoots.Should().Contain("Rx-Main 2.2.5 - NuGet");
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

        // 11 resolved packages (4-space lines only)
        detectedComponents.Should().HaveCount(11);

        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Core-log4net 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.LoggingFacility 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Windsor 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Castle.Windsor-log4net 3.3.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Core 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Interfaces 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Linq 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-Main 2.2.5"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Rx-PlatformServices 2.2.5"));
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
    public async Task TestPaketDetector_VersionWithPreReleaseAndBuildMetadata()
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
    PackageB (2.1.0)
    PackageC (2.9.0)
    PackageD (1.5.3)
    PackageE (1.2.3)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // All 5 resolved packages should be detected with their actual resolved versions
        detectedComponents.Should().HaveCount(5);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("PackageA 1.0.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("PackageB 2.1.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("PackageC 2.9.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("PackageD 1.5.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("PackageE 1.2.3"));
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
    Microsoft.Extensions.DependencyInjection.Abstractions (8.0.1)
    Microsoft.Extensions.Logging.Abstractions (8.0.1)
      Microsoft.Extensions.DependencyInjection.Abstractions (>= 8.0.1)
    Newtonsoft.Json (13.0.3)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(4);
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

        // FSharp.Core appears in both groups with different versions; both are registered.
        // Build group has 9.0.300, Server group has 9.0.303.
        detectedComponents.Should().HaveCount(4);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core 9.0.300"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core 9.0.303"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Newtonsoft.Json 13.0.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Azure.Core 1.46.1"));

        // Build is a well-known dev group; Server is not
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Core 9.0.300 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("Newtonsoft.Json 13.0.3 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("Azure.Core 1.46.1 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Core 9.0.303 - NuGet").Should().BeFalse();
    }

    [TestMethod]
    public async Task TestPaketDetector_WithDependencyRestrictions()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    Azure.Core (1.46.1) - restriction: || (&& (>= net462) (>= netstandard2.0)) (>= net8.0)
      Microsoft.Bcl.AsyncInterfaces (>= 8.0) - restriction: || (>= net462) (>= netstandard2.0)
      System.Memory.Data (>= 6.0.1) - restriction: || (>= net462) (>= netstandard2.0)
    Microsoft.Bcl.AsyncInterfaces (8.0.0)
    System.Memory.Data (6.0.1)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // All 3 resolved packages detected with correct versions
        detectedComponents.Should().HaveCount(3);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Azure.Core 1.46.1"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Microsoft.Bcl.AsyncInterfaces 8.0.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("System.Memory.Data 6.0.1"));
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

        // Should detect 2 resolved packages regardless of STORAGE directive
        detectedComponents.Should().HaveCount(2);
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
    Fake.Core.CommandLineParsing (6.1.3)
    Fake.Core.Context (6.1.3)
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
    FSharp.Data.Csv.Core (6.6)
    FSharp.Core (9.0.303)
    Microsoft.AspNetCore.Http.Connections (1.2)
    Microsoft.AspNetCore.SignalR (1.2)
      Microsoft.AspNetCore.Http.Connections (>= 1.2) - restriction: >= netstandard2.0
    Serilog (4.2) - restriction: || (>= net462) (>= netstandard2.0)
      System.Diagnostics.DiagnosticSource (>= 8.0.1) - restriction: || (&& (>= net462) (< netstandard2.0)) (&& (< net462) (< net6.0) (>= netstandard2.0)) (>= net471)
    System.Diagnostics.DiagnosticSource (8.0.1)

GROUP Test
NUGET
  remote: https://api.nuget.org/v3/index.json
    NUnit (4.3.2)
      System.Memory (>= 4.6) - restriction: >= net462
    NUnit3TestAdapter (5.0)
    System.Memory (4.6)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should detect all resolved packages from all groups.
        // FSharp.Core appears in Build (9.0.300) and Server (9.0.303) with different versions.
        detectedComponents.Should().HaveCount(14);
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Fake.Core.Target 6.1.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Fake.Core.CommandLineParsing 6.1.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Fake.Core.Context 6.1.3"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core 9.0.300"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Data 6.6"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Data.Csv.Core 6.6"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("FSharp.Core 9.0.303"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Microsoft.AspNetCore.SignalR 1.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Microsoft.AspNetCore.Http.Connections 1.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("Serilog 4.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("System.Diagnostics.DiagnosticSource 8.0.1"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("NUnit 4.3.2"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("NUnit3TestAdapter 5.0"));
        detectedComponents.Should().Contain(c => c.Component.Id.Contains("System.Memory 4.6"));

        // Build group is a well-known dev group
        componentRecorder.GetEffectiveDevDependencyValue("Fake.Core.Target 6.1.3 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("Fake.Core.CommandLineParsing 6.1.3 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("Fake.Core.Context 6.1.3 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Core 9.0.300 - NuGet").Should().BeTrue();

        // Server group is NOT a well-known dev group
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Data 6.6 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Data.Csv.Core 6.6 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Core 9.0.303 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("Microsoft.AspNetCore.SignalR 1.2 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("Microsoft.AspNetCore.Http.Connections 1.2 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("Serilog 4.2 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("System.Diagnostics.DiagnosticSource 8.0.1 - NuGet").Should().BeFalse();

        // Test group is a well-known dev group
        componentRecorder.GetEffectiveDevDependencyValue("NUnit 4.3.2 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("NUnit3TestAdapter 5.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("System.Memory 4.6 - NuGet").Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPaketDetector_UnresolvedDependencyIsIgnored()
    {
        // If a 6-space dependency doesn't have a corresponding 4-space resolved entry,
        // it should be silently ignored (not registered with a fake version from the constraint)
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    PackageA (1.0.0)
      NonExistentPackage (>= 2.0.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Only the resolved package should be detected, not the unresolved dependency
        detectedComponents.Should().ContainSingle(c => c.Component.Id.Contains("PackageA 1.0.0"));
        detectedComponents.Should().NotContain(c => ((NuGetComponent)c.Component).Name == "NonExistentPackage");
    }

    [TestMethod]
    public async Task TestPaketDetector_DefaultGroupIsNotDevDependency()
    {
        var paketLock = @"NUGET
  remote: https://api.nuget.org/v3/index.json
    Newtonsoft.Json (13.0.3)
    FSharp.Core (8.0.200)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(2);

        // Default (unnamed) group packages are production dependencies
        componentRecorder.GetEffectiveDevDependencyValue("Newtonsoft.Json 13.0.3 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Core 8.0.200 - NuGet").Should().BeFalse();
    }

    [TestMethod]
    public async Task TestPaketDetector_MainGroupIsNotDevDependency()
    {
        var paketLock = @"GROUP Main
NUGET
  remote: https://api.nuget.org/v3/index.json
    Newtonsoft.Json (13.0.3)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        componentRecorder.GetEffectiveDevDependencyValue("Newtonsoft.Json 13.0.3 - NuGet").Should().BeFalse();
    }

    [TestMethod]
    public async Task TestPaketDetector_TestGroupIsDevDependency()
    {
        var paketLock = @"GROUP Test
NUGET
  remote: https://api.nuget.org/v3/index.json
    NUnit (4.3.2)
    Moq (4.20.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        componentRecorder.GetEffectiveDevDependencyValue("NUnit 4.3.2 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("Moq 4.20.0 - NuGet").Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPaketDetector_SuffixTestGroupIsDevDependency()
    {
        // Groups ending with "test" or "tests" should be dev dependencies (e.g., UnitTest, IntegrationTests)
        var paketLock = @"GROUP UnitTest
NUGET
  remote: https://api.nuget.org/v3/index.json
    xunit (2.9.0)

GROUP IntegrationTests
NUGET
  remote: https://api.nuget.org/v3/index.json
    FluentAssertions (6.12.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        componentRecorder.GetEffectiveDevDependencyValue("xunit 2.9.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("FluentAssertions 6.12.0 - NuGet").Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPaketDetector_AllWellKnownDevGroupNames()
    {
        // Verify all well-known group names are recognized as dev dependencies
        var paketLock = @"GROUP Tests
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgTests (1.0.0)

GROUP Docs
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgDocs (1.0.0)

GROUP Documentation
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgDocumentation (1.0.0)

GROUP Build
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgBuild (1.0.0)

GROUP Analyzers
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgAnalyzers (1.0.0)

GROUP Fake
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgFake (1.0.0)

GROUP Benchmark
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgBenchmark (1.0.0)

GROUP Benchmarks
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgBenchmarks (1.0.0)

GROUP Samples
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgSamples (1.0.0)

GROUP DesignTime
NUGET
  remote: https://api.nuget.org/v3/index.json
    PkgDesignTime (1.0.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        detectedComponents.Should().HaveCount(10);

        // All well-known dev group packages should be dev dependencies
        componentRecorder.GetEffectiveDevDependencyValue("PkgTests 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgDocs 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgDocumentation 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgBuild 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgAnalyzers 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgFake 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgBenchmark 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgBenchmarks 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgSamples 1.0.0 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("PkgDesignTime 1.0.0 - NuGet").Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPaketDetector_UnknownGroupIsNotDevDependency()
    {
        // Non-well-known group names should not be treated as dev dependencies
        var paketLock = @"GROUP Server
NUGET
  remote: https://api.nuget.org/v3/index.json
    Giraffe (6.0.0)

GROUP Client
NUGET
  remote: https://api.nuget.org/v3/index.json
    Fable.Core (4.0.0)

GROUP Shared
NUGET
  remote: https://api.nuget.org/v3/index.json
    Thoth.Json (7.0.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        componentRecorder.GetEffectiveDevDependencyValue("Giraffe 6.0.0 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("Fable.Core 4.0.0 - NuGet").Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue("Thoth.Json 7.0.0 - NuGet").Should().BeFalse();
    }

    [TestMethod]
    public async Task TestPaketDetector_SamePackageSameVersionInDevAndProdGroups()
    {
        // When the same package with the same version appears in both a dev group and a prod group,
        // the framework's AND-merge ensures the final result is false (production wins).
        var paketLock = @"GROUP Main
NUGET
  remote: https://api.nuget.org/v3/index.json
    FSharp.Core (9.0.300)

GROUP Test
NUGET
  remote: https://api.nuget.org/v3/index.json
    FSharp.Core (9.0.300)
    NUnit (4.3.2)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // FSharp.Core appears in both Main (prod) and Test (dev) with the same version.
        // The AND-merge means it's NOT a dev dependency (production usage wins).
        componentRecorder.GetEffectiveDevDependencyValue("FSharp.Core 9.0.300 - NuGet").Should().BeFalse();

        // NUnit only appears in Test, so it remains a dev dependency
        componentRecorder.GetEffectiveDevDependencyValue("NUnit 4.3.2 - NuGet").Should().BeTrue();
    }

    [TestMethod]
    public async Task TestPaketDetector_DevGroupNameMatchingIsCaseInsensitive()
    {
        var paketLock = @"GROUP TEST
NUGET
  remote: https://api.nuget.org/v3/index.json
    NUnit (4.3.2)

GROUP build
NUGET
  remote: https://api.nuget.org/v3/index.json
    Fake.Core.Target (6.1.3)

GROUP INTEGRATIONTESTS
NUGET
  remote: https://api.nuget.org/v3/index.json
    FluentAssertions (6.12.0)
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("paket.lock", paketLock)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        componentRecorder.GetEffectiveDevDependencyValue("NUnit 4.3.2 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("Fake.Core.Target 6.1.3 - NuGet").Should().BeTrue();
        componentRecorder.GetEffectiveDevDependencyValue("FluentAssertions 6.12.0 - NuGet").Should().BeTrue();
    }

    [TestMethod]
    public void TestIsDevelopmentDependencyGroup_WellKnownNames()
    {
        // Exact matches
        PaketComponentDetector.IsDevelopmentDependencyGroup("test").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Test").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("TEST").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("tests").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Tests").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("docs").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Docs").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("documentation").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Documentation").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("build").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Build").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("analyzers").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Analyzers").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("fake").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Fake").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("benchmark").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("benchmarks").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("samples").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("designtime").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("DesignTime").Should().BeTrue();

        // Suffix matches
        PaketComponentDetector.IsDevelopmentDependencyGroup("UnitTest").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("unittest").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("IntegrationTest").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("UnitTests").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("IntegrationTests").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("AcceptanceTests").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("E2ETest").Should().BeTrue();
        PaketComponentDetector.IsDevelopmentDependencyGroup("SmokeTests").Should().BeTrue();

        // Non-dev groups
        PaketComponentDetector.IsDevelopmentDependencyGroup(string.Empty).Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Main").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("main").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Server").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Client").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Shared").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Web").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Api").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Core").Should().BeFalse();
        PaketComponentDetector.IsDevelopmentDependencyGroup("Infrastructure").Should().BeFalse();
    }
}

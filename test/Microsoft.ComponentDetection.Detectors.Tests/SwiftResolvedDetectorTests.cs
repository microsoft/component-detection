#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Swift;

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Swift;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SwiftResolvedDetectorTests : BaseDetectorTest<SwiftResolvedComponentDetector>
{
    [TestMethod]
    public async Task Test_GivenDetectorWithValidFile_WhenScan_ThenScanIsSuccessfulAndComponentsAreRegistered()
    {
        var validResolvedPackageFile = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "version" : "5.9.1"
            }
        }
    ],
    "version" : 2
}
""";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            validResolvedPackageFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Two components are detected because this detector registers a SwiftPM and Git component.
        detectedComponents.Should().HaveCount(2);

        var typedComponents = detectedComponents.Select(c => c.Component).ToList();

        typedComponents.Should().ContainEquivalentOf(
            new SwiftComponent(
                name: "alamofire",
                version: "5.9.1",
                packageUrl: "https://github.com/Alamofire/Alamofire",
                hash: "f455c2975872ccd2d9c81594c658af65716e9b9a"));

        typedComponents.Should().ContainEquivalentOf(
            new GitComponent(
                repositoryUrl: new Uri("https://github.com/Alamofire/Alamofire"),
                commitHash: "f455c2975872ccd2d9c81594c658af65716e9b9a",
                tag: "5.9.1"));
    }

    // Test for several packages
    [TestMethod]
    public async Task Test_GivenDetectorWithValidFileWithMultiplePackages_WhenScan_ThenScanIsSuccessfulAndComponentsAreRegistered()
    {
        var validLongResolvedPackageFile = this.validLongResolvedPackageFile;

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            validLongResolvedPackageFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Two components are detected because this detector registers a SwiftPM and Git component.
        detectedComponents.Should().HaveCount(6);

        var typedComponents = detectedComponents.Select(c => c.Component).ToList();

        typedComponents.Should().ContainEquivalentOf(
            new SwiftComponent(
                name: "alamofire",
                version: "5.6.0",
                packageUrl: "https://github.com/Alamofire/Alamofire",
                hash: "63dfa86548c4e5d5c6fd6ed42f638e388cbce529"));

        typedComponents.Should().ContainEquivalentOf(
            new GitComponent(
                repositoryUrl: new Uri("https://github.com/sideeffect-io/AsyncExtensions"),
                commitHash: "3442d3d046800f1974bda096faaf0ac510b21154",
                tag: "0.5.3"));

        typedComponents.Should().ContainEquivalentOf(
            new GitComponent(
                repositoryUrl: new Uri("https://github.com/devicekit/DeviceKit.git"),
                commitHash: "d37e70cb2646666dcf276d7d3d4a9760a41ff8a6",
                tag: "4.9.0"));
    }

    // Duplicate packages
    [TestMethod]
    public async Task Test_GivenDetectorWithValidFileWithDuplicatePackages_WhenScan_ThenScanIsSuccessfulAndComponentsAreRegisteredAndComponentsAreNotDuplicate()
    {
        var duplicatePackages = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "version" : "5.9.1"
            }
        },
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "version" : "5.9.1"
            }
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            duplicatePackages)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Two components are detected because this detector registers a SwiftPM and Git component.
        // The duplicate package is not registered.
        detectedComponents.Should().HaveCount(2);

        var typedComponents = detectedComponents.Select(c => c.Component).ToList();

        typedComponents.Should().ContainEquivalentOf(
            new SwiftComponent(
                name: "alamofire",
                version: "5.9.1",
                packageUrl: "https://github.com/Alamofire/Alamofire",
                hash: "f455c2975872ccd2d9c81594c658af65716e9b9a"));

        typedComponents.Should().ContainEquivalentOf(
            new GitComponent(
                repositoryUrl: new Uri("https://github.com/Alamofire/Alamofire"),
                commitHash: "f455c2975872ccd2d9c81594c658af65716e9b9a",
                tag: "5.9.1"));
    }

    [TestMethod]
    public async Task Test_GivenInvalidJSONFile_WhenScan_ThenNoComponentRegisteredAndScanIsSuccessful()
    {
        var invalidJSONResolvedPackageFile = """
{
 INVALID JSON
}
""";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            invalidJSONResolvedPackageFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenEmptyFile_WhenScan_ThenNoComponentRegisteredAndScanIsSuccessful()
    {
        var emptyResolvedPackageFile = string.Empty;
        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            emptyResolvedPackageFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResvoledPackageWithoutPins_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var resolvedPackageWithoutPins = """
{
    "pins" : [
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithoutPins)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithoutIdentity_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var validResolvedPackageFile = """
{
    "pins" : [
        {
            "identity" : "",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "version" : "5.9.1"
            }
        },
        {
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/SimplyDanny/SwiftLintPlugins",
            "state" : {
                "revision" : "6c3d6c32a37224179dc290f21e03d1238f3d963b",
                "version" : "0.56.2"
            }
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            validResolvedPackageFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithoutKind_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var resolvedPackageWithoutKind = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "version" : "5.9.1"
            }
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithoutKind)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithoutLocation_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var resolvedPackageWithoutLocation = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "version" : "5.9.1"
            }
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithoutLocation)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithoutState_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var resolvedPackageWithoutState = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire"
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithoutState)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithEmptyState_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var resolvedPackageWithEmptyState = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {}
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithEmptyState)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithoutRevision_WhenScan_ThenScanIsSuccessfulAndNoComponentsRegistered()
    {
        var resolvedPackageWithoutRevision = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "version" : "5.9.1"
            }
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithoutRevision)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Test_GivenResolvedPackageWithoutVersion_WhenScan_ThenScanIsSuccessfulAndComponentRegisteredWithRevisionHashAsVersion()
    {
        var resolvedPackageWithoutVersion = """
{
    "pins" : [
        {
            "identity" : "alamofire",
            "kind" : "remoteSourceControl",
            "location" : "https://github.com/Alamofire/Alamofire",
            "state" : {
                "revision" : "f455c2975872ccd2d9c81594c658af65716e9b9a"
            }
        }
    ],
    "version" : 2
}
""";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility.WithFile(
            "Package.resolved",
            resolvedPackageWithoutVersion)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var typedComponents = detectedComponents.Select(c => c.Component).ToList();

        typedComponents.Should().ContainEquivalentOf(
            new SwiftComponent(
                name: "alamofire",
                version: "f455c2975872ccd2d9c81594c658af65716e9b9a",
                packageUrl: "https://github.com/Alamofire/Alamofire",
                hash: "f455c2975872ccd2d9c81594c658af65716e9b9a"));

        typedComponents.Should().ContainEquivalentOf(
            new GitComponent(
                repositoryUrl: new Uri("https://github.com/Alamofire/Alamofire"),
                commitHash: "f455c2975872ccd2d9c81594c658af65716e9b9a",
                tag: "f455c2975872ccd2d9c81594c658af65716e9b9a"));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "Test data that is better placed at the end of the file.")]
    private readonly string validLongResolvedPackageFile = """
{
  "originHash" : "6ad1e0d3ae43bde33043d3286afc3d98e5be09945ac257218cb6a9dba14466c3",
  "pins" : [
    {
      "identity" : "alamofire",
      "kind" : "remoteSourceControl",
      "location" : "https://github.com/Alamofire/Alamofire",
      "state" : {
        "revision" : "63dfa86548c4e5d5c6fd6ed42f638e388cbce529",
        "version" : "5.6.0"
      }
    },
    {
      "identity" : "asyncextensions",
      "kind" : "remoteSourceControl",
      "location" : "https://github.com/sideeffect-io/AsyncExtensions",
      "state" : {
        "branch": null,
        "revision" : "3442d3d046800f1974bda096faaf0ac510b21154",
        "version" : "0.5.3"
      }
    },
    {
      "identity" : "devicekit",
      "kind" : "remoteSourceControl",
      "location" : "https://github.com/devicekit/DeviceKit.git",
      "state" : {
        "revision" : "d37e70cb2646666dcf276d7d3d4a9760a41ff8a6",
        "version" : "4.9.0"
      }
    },
    {
      "identity" : "localdependency",
      "kind" : "localSource",
      "location" : "../LocalDependency"
    }
  ],
  "version" : 2
}
"""
;
}

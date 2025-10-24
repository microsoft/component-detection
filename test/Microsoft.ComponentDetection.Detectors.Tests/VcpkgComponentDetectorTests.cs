#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Vcpkg;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class VcpkgComponentDetectorTests : BaseDetectorTest<VcpkgComponentDetector>
{
    private readonly Mock<ICommandLineInvocationService> mockCommandLineInvocationService;
    private readonly Mock<IEnvironmentVariableService> mockEnvironmentVariableService;

    public VcpkgComponentDetectorTests()
    {
        this.mockCommandLineInvocationService = new Mock<ICommandLineInvocationService>();
        this.DetectorTestUtility.AddServiceMock(this.mockCommandLineInvocationService);

        this.mockEnvironmentVariableService = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.mockEnvironmentVariableService);

        var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
        this.DetectorTestUtility.WithScanRequest(
            new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                null,
                null,
                new Dictionary<string, string>(),
                null,
                componentRecorder));
    }

    [TestMethod]
    public async Task TestNlohmannAsync()
    {
        var spdxFile = @"{
    ""SPDXID"": ""SPDXRef - DOCUMENT"",
    ""documentNamespace"":
        ""https://spdx.org/spdxdocs/nlohmann-json-x64-linux-3.10.4-78c7f190-b402-44d1-a364-b9ac86392b84"",
    ""name"": ""nlohmann-json:x64-linux@3.10.4 69dcfc6886529ad2d210f71f132d743672a7e65d2c39f53456f17fc5fc08b278"",
    ""packages"": [
        {
            ""name"": ""nlohmann-json"",
            ""SPDXID"": ""SPDXRef-port"",
            ""versionInfo"": ""3.10.4#5"",
            ""downloadLocation"": ""git+https://github.com/Microsoft/vcpkg#ports/nlohmann-json"",
            ""homepage"": ""https://github.com/nlohmann/json"",
            ""licenseConcluded"": ""NOASSERTION"",
            ""licenseDeclared"": ""NOASSERTION"",
            ""copyrightText"": ""NOASSERTION"",
            ""description"": ""JSON for Modern C++"",
            ""comment"": ""This is the port (recipe) consumed by vcpkg.""
        }
    ]
}";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        var sbomComponent = (VcpkgComponent)components.FirstOrDefault()?.Component;

        sbomComponent.Should().NotBeNull();

        components.Should().ContainSingle();
        sbomComponent.Id.Should().Be("git+https://github.com/Microsoft/vcpkg#ports/nlohmann-json : nlohmann-json 3.10.4#5 - Vcpkg");
        sbomComponent.Name.Should().Be("nlohmann-json");
        sbomComponent.Version.Should().Be("3.10.4");
        sbomComponent.PortVersion.Should().Be(5);
        sbomComponent.SPDXID.Should().Be("SPDXRef-port");
        sbomComponent.DownloadLocation.Should().Be("git+https://github.com/Microsoft/vcpkg#ports/nlohmann-json");
        sbomComponent.PackageUrl.ToString().Should().Be("pkg:vcpkg/nlohmann-json@3.10.4?port_version=5");
    }

    [TestMethod]
    public async Task TestTinyxmlAndResourceAsync()
    {
        var spdxFile = @"{
    ""SPDXID"": ""SPDXRef - DOCUMENT"",
    ""documentNamespace"":
        ""https://spdx.org/spdxdocs/tinyxml2-x64-linux-9.0.0-c99e4f03-5275-458b-8a69-b5f8dfa45f18"",
    ""name"": ""tinyxml2:x64-linux@9.0.0 5c7679507def92c5c71df44aec08a90a5c749f7f805b3f0e8e70f5e8a5b1b8d0"",
    ""packages"": [
        {
            ""name"": ""tinyxml2:x64-linux"",
            ""SPDXID"": ""SPDXRef-binary"",
            ""versionInfo"": ""5c7679507def92c5c71df44aec08a90a5c749f7f805b3f0e8e70f5e8a5b1b8d0"",
            ""downloadLocation"": ""NONE"",
            ""licenseConcluded"": ""NOASSERTION"",
            ""licenseDeclared"": ""NOASSERTION"",
            ""copyrightText"": ""NOASSERTION"",
            ""comment"": ""This is a binary package built by vcpkg.""
        },
        {
            ""SPDXID"": ""SPDXRef-resource-1"",
            ""name"": ""leethomason/tinyxml2"",
            ""downloadLocation"": ""git+https://github.com/leethomason/tinyxml2@9.0.0"",
            ""licenseConcluded"": ""NOASSERTION"",
            ""licenseDeclared"": ""NOASSERTION"",
            ""copyrightText"": ""NOASSERTION"",
            ""checksums"": [
                {
                    ""algorithm"": ""SHA512"",
                    ""checksumValue"": ""9c5ce8131984690df302ca3e32314573b137180ed522c92fd631692979c942372a28f697fdb3d5e56bcf2d3dc596262b724d088153f3e1d721c9536f2a883367""
                }
            ]
        }
    ]
}";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();

        components.Should().HaveCount(2);
        var sbomComponent = (VcpkgComponent)components.FirstOrDefault(c => ((VcpkgComponent)c?.Component).SPDXID.Equals("SPDXRef-binary")).Component;
        sbomComponent.Should().NotBeNull();
        sbomComponent.Id.Should().Be("tinyxml2:x64-linux 5c7679507def92c5c71df44aec08a90a5c749f7f805b3f0e8e70f5e8a5b1b8d0 - Vcpkg");
        sbomComponent.Name.Should().Be("tinyxml2:x64-linux");
        sbomComponent.Version.Should().Be("5c7679507def92c5c71df44aec08a90a5c749f7f805b3f0e8e70f5e8a5b1b8d0");
        sbomComponent.SPDXID.Should().Be("SPDXRef-binary");
        sbomComponent.DownloadLocation.Should().Be("NONE");

        sbomComponent = (VcpkgComponent)components.FirstOrDefault(c => ((VcpkgComponent)c.Component).SPDXID.Equals("SPDXRef-resource-1")).Component;
        sbomComponent.Id.Should().Be("git+https://github.com/leethomason/tinyxml2 : leethomason/tinyxml2 9.0.0 - Vcpkg");
        sbomComponent.Name.Should().Be("leethomason/tinyxml2");
        sbomComponent.Version.Should().Be("9.0.0");
        sbomComponent.SPDXID.Should().Be("SPDXRef-resource-1");
        sbomComponent.DownloadLocation.Should().Be("git+https://github.com/leethomason/tinyxml2");
    }

    [TestMethod]
    public async Task TestBlankJsonAsync()
    {
        var spdxFile = "{}";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        components.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestInvalidFileAsync()
    {
        var spdxFile = "invalidspdxfile";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        components.Should().BeEmpty();
    }

    [TestMethod]
    [DataTestMethod]
    [DataRow("vcpkg_installed\\manifest-info.json", "vcpkg.json")]
    [DataRow("vcpkg_installed\\vcpkg\\manifest-info.json", "vcpkg.json")]
    [DataRow("bad_location\\manifest-info.json", "vcpkg_installed\\packageLocation\\vcpkg.spdx.json")]
    public async Task TestVcpkgManifestFileAsync(string manifestPath, string pathToVcpkg)
    {
        var t_pathToVcpkg = CrossPlatformPath(Path.GetFullPath(pathToVcpkg));
        var t_manifestPath = CrossPlatformPath(Path.GetFullPath(manifestPath));

        var spdxFile = @"{
    ""SPDXID"": ""SPDXRef - DOCUMENT"",
    ""documentNamespace"":
        ""https://spdx.org/spdxdocs/nlohmann-json-x64-linux-3.10.4-78c7f190-b402-44d1-a364-b9ac86392b84"",
    ""name"": ""nlohmann-json:x64-linux@3.10.4 69dcfc6886529ad2d210f71f132d743672a7e65d2c39f53456f17fc5fc08b278"",
    ""packages"": [
        {
            ""name"": ""nlohmann-json"",
            ""SPDXID"": ""SPDXRef-port"",
            ""versionInfo"": ""3.10.4#5"",
            ""downloadLocation"": ""git+https://github.com/Microsoft/vcpkg#ports/nlohmann-json"",
            ""homepage"": ""https://github.com/nlohmann/json"",
            ""licenseConcluded"": ""NOASSERTION"",
            ""licenseDeclared"": ""NOASSERTION"",
            ""copyrightText"": ""NOASSERTION"",
            ""description"": ""JSON for Modern C++"",
            ""comment"": ""This is the port (recipe) consumed by vcpkg.""
        }
    ]
}";
        var manifestFile = $@"{{
    ""manifest-path"": ""{t_pathToVcpkg.Replace("\\", "\\\\")}""
}}";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(CrossPlatformPath(Path.GetFullPath("vcpkg_installed\\packageLocation\\vcpkg.spdx.json")), spdxFile)
            .WithFile(t_manifestPath, manifestFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDependencyGraphsByLocation();

        var singleFileComponent = detectedComponents.FirstOrDefault();
        singleFileComponent.Should().NotBeNull();

        var expectedResult = singleFileComponent.Key.Replace("/tmp/", string.Empty);
        expectedResult.Should().Be(t_pathToVcpkg);
    }

    private static string CrossPlatformPath(string relPath)
    {
        var segments = relPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine(segments);
    }
}

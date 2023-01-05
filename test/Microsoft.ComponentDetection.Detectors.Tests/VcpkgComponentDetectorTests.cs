using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Vcpkg;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class VcpkgComponentDetectorTests
{
    private DetectorTestUtility<VcpkgComponentDetector> detectorTestUtility;

    [TestInitialize]
    public void TestInitialize()
    {
        var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
        this.detectorTestUtility = DetectorTestUtilityCreator.Create<VcpkgComponentDetector>()
            .WithScanRequest(new ScanRequest(new DirectoryInfo(Path.GetTempPath()), null, null, new Dictionary<string, string>(), null, componentRecorder));
    }

    [TestMethod]
    public async Task TestNlohmann()
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
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetector();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        var sbomComponent = (VcpkgComponent)components.FirstOrDefault()?.Component;

        if (sbomComponent is null)
        {
            throw new AssertFailedException($"{nameof(sbomComponent)} is null");
        }

        Assert.AreEqual(1, components.Count);
        Assert.AreEqual("nlohmann-json", sbomComponent.Name);
        Assert.AreEqual("3.10.4", sbomComponent.Version);
        Assert.AreEqual(5, sbomComponent.PortVersion);
        Assert.AreEqual("SPDXRef-port", sbomComponent.SPDXID);
        Assert.AreEqual("git+https://github.com/Microsoft/vcpkg#ports/nlohmann-json", sbomComponent.DownloadLocation);
        Assert.AreEqual("pkg:vcpkg/nlohmann-json@3.10.4?port_version=5", sbomComponent.PackageUrl.ToString());
    }

    [TestMethod]
    public async Task TestTinyxmlAndResource()
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
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetector();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();

        Assert.AreEqual(2, components.Count);
        var sbomComponent = (VcpkgComponent)components.FirstOrDefault(c => ((VcpkgComponent)c?.Component).SPDXID.Equals("SPDXRef-binary")).Component;
        Assert.IsNotNull(sbomComponent);
        Assert.AreEqual("tinyxml2:x64-linux", sbomComponent.Name);
        Assert.AreEqual("5c7679507def92c5c71df44aec08a90a5c749f7f805b3f0e8e70f5e8a5b1b8d0", sbomComponent.Version);
        Assert.AreEqual("SPDXRef-binary", sbomComponent.SPDXID);
        Assert.AreEqual("NONE", sbomComponent.DownloadLocation);

        sbomComponent = (VcpkgComponent)components.FirstOrDefault(c => ((VcpkgComponent)c.Component).SPDXID.Equals("SPDXRef-resource-1")).Component;
        Assert.AreEqual("leethomason/tinyxml2", sbomComponent.Name);
        Assert.AreEqual("9.0.0", sbomComponent.Version);
        Assert.AreEqual("SPDXRef-resource-1", sbomComponent.SPDXID);
        Assert.AreEqual("git+https://github.com/leethomason/tinyxml2", sbomComponent.DownloadLocation);
    }

    [TestMethod]
    public async Task TestBlankJson()
    {
        var spdxFile = "{}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetector();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        Assert.IsFalse(components.Any());
    }

    [TestMethod]
    public async Task TestInvalidFile()
    {
        var spdxFile = "invalidspdxfile";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("vcpkg.spdx.json", spdxFile)
            .ExecuteDetector();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        Assert.IsFalse(components.Any());
    }
}

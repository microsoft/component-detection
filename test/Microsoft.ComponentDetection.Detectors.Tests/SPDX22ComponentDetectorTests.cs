#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Spdx;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class Spdx22ComponentDetectorTests
{
    private readonly DetectorTestUtilityBuilder<Spdx22ComponentDetector> detectorTestUtility = new();

    public Spdx22ComponentDetectorTests()
    {
        var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
        this.detectorTestUtility.WithScanRequest(
            new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                null,
                null,
                new Dictionary<string, string>(),
                null,
                componentRecorder));
    }

    [TestMethod]
    public async Task TestSbomDetector_SimpleSbomAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""files"": [{
        ""fileName"": ""./.eslintrc.js"",
        ""SPDXID"": ""SPDXRef-File--.eslintrc.js-76586927C59544FB23BE1CF4D269882217EE21AB"",
        ""checksums"": [
            {
                ""algorithm"": ""SHA256"",
                ""checksumValue"": ""5c9c9d7eb9d31320bd621b3089ec0ae87d97e9ee7ed03cde2d20383928f72958""
            },
            {
                ""algorithm"": ""SHA1"",
                ""checksumValue"": ""76586927c59544fb23be1cf4d269882217ee21ab""
            }
        ],
        ""licenseConcluded"": ""NOASSERTION"",
        ""licenseInfoInFile"": [
            ""NOASSERTION""
        ],
        ""copyrightText"": ""NOASSERTION""
    }],
    ""packages"": [
        {
            ""name"": ""Test"",
            ""SPDXID"": ""SPDXRef-RootPackage"",
            ""downloadLocation"": ""NOASSERTION"",
            ""packageVerificationCode"": {
                ""packageVerificationCodeValue"": ""12fa1211046c12118936384b6c8683f1ac9b790a""
            },
            ""filesAnalyzed"": true,
            ""licenseConcluded"": ""NOASSERTION"",
            ""licenseInfoFromFiles"": [
                ""NOASSERTION""
            ],
            ""licenseDeclared"": ""NOASSERTION"",
            ""copyrightText"": ""NOASSERTION"",
            ""versionInfo"": ""1.0.0"",
            ""supplier"": ""Organization: Microsoft"",
            ""hasFiles"": [""SPDXRef-File--.eslintrc.js-76586927C59544FB23BE1CF4D269882217EE21AB""]
        }
    ],
    ""relationships"": [
        {
            ""relationshipType"": ""DESCRIBES"",
            ""relatedSpdxElement"": ""SPDXRef-RootPackage"",
            ""spdxElementId"": ""SPDXRef-DOCUMENT""
        }
    ],
    ""spdxVersion"": ""SPDX-2.2"",
    ""dataLicense"": ""CC0-1.0"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""Test 1.0.0"",
    ""documentNamespace"": ""https://sbom.microsoft/Test/1.0.0/61de1a5-57cc-4732-9af5-edb321b4a7ee"",
    ""creationInfo"": {
        ""created"": ""2022-02-14T20:26:41Z"",
        ""creators"": [
            ""Organization: Microsoft"",
            ""Tool: Microsoft.SBOMTool-1.0.0""
        ]
    },
    ""documentDescribes"": [
        ""SPDXRef-RootPackage""
    ]
}";

        var spdxFileName = "manifest.spdx.json";
        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile(spdxFileName, spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        var sbomComponent = (SpdxComponent)components.FirstOrDefault()?.Component;

        sbomComponent.Should().NotBeNull();

#pragma warning disable CA5350 // Suppress Do Not Use Weak Cryptographic Algorithms because we use SHA1 intentionally in SPDX format
        var checksum = BitConverter.ToString(SHA1.HashData(Encoding.UTF8.GetBytes(spdxFile))).Replace("-", string.Empty).ToLower();
#pragma warning restore CA5350

        components.Should().ContainSingle();
        sbomComponent.Name.Should().Be("Test 1.0.0");
        sbomComponent.RootElementId.Should().Be("SPDXRef-RootPackage");
        sbomComponent.DocumentNamespace.Should().Be(new Uri("https://sbom.microsoft/Test/1.0.0/61de1a5-57cc-4732-9af5-edb321b4a7ee"));
        sbomComponent.SpdxVersion.Should().Be("SPDX-2.2");
        sbomComponent.Checksum.Should().Be(checksum);
        sbomComponent.Path.Should().Be(Path.Combine(Path.GetTempPath(), spdxFileName));

        sbomComponent.CreatorTool.Should().Be("Microsoft.SBOMTool-1.0.0");
        sbomComponent.CreatorOrganization.Should().Be("Microsoft");
    }

    [TestMethod]
    public async Task TestSbomDetector_BlankJsonAsync()
    {
        var spdxFile = "{}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        components.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestSbomDetector_InvalidFileAsync()
    {
        var spdxFile = "invalidspdxfile";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var components = detectedComponents.ToList();
        components.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestSbomDetector_ExtractsCreatorToolAndOrganizationAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""creationInfo"": {
        ""created"": ""2024-01-01T00:00:00Z"",
        ""creators"": [
            ""Tool: microsoft/sbom-tool-2.2.0"",
            ""Organization: Microsoft""
        ]
    },
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().Be("microsoft/sbom-tool-2.2.0");
        sbomComponent.CreatorOrganization.Should().Be("Microsoft");
    }

    [TestMethod]
    public async Task TestSbomDetector_MissingCreationInfoReturnsNullAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().BeNull();
        sbomComponent.CreatorOrganization.Should().BeNull();
    }

    [TestMethod]
    public async Task TestSbomDetector_WhitespaceOnlyCreatorsReturnNullAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""creationInfo"": {
        ""created"": ""2024-01-01T00:00:00Z"",
        ""creators"": [
            ""Tool:   "",
            ""Organization:   ""
        ]
    },
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().BeNull();
        sbomComponent.CreatorOrganization.Should().BeNull();
    }

    [TestMethod]
    public async Task TestSbomDetector_MultipleCreatorsPicksFirstToolAndOrgAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""creationInfo"": {
        ""created"": ""2024-01-01T00:00:00Z"",
        ""creators"": [
            ""Tool: first-tool-1.0"",
            ""Tool: second-tool-2.0"",
            ""Organization: FirstOrg"",
            ""Organization: SecondOrg""
        ]
    },
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().Be("first-tool-1.0");
        sbomComponent.CreatorOrganization.Should().Be("FirstOrg");
    }

    [TestMethod]
    public async Task TestSbomDetector_OnlyToolNoOrganizationAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""creationInfo"": {
        ""created"": ""2024-01-01T00:00:00Z"",
        ""creators"": [
            ""Tool: my-tool-3.0""
        ]
    },
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().Be("my-tool-3.0");
        sbomComponent.CreatorOrganization.Should().BeNull();
    }

    [TestMethod]
    public async Task TestSbomDetector_OnlyOrganizationNoToolAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""creationInfo"": {
        ""created"": ""2024-01-01T00:00:00Z"",
        ""creators"": [
            ""Organization: Contoso""
        ]
    },
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().BeNull();
        sbomComponent.CreatorOrganization.Should().Be("Contoso");
    }

    [TestMethod]
    public async Task TestSbomDetector_CreatorsWithNoToolOrOrgPrefixAsync()
    {
        var spdxFile = /*lang=json,strict*/ @"{
    ""spdxVersion"": ""SPDX-2.2"",
    ""SPDXID"": ""SPDXRef-DOCUMENT"",
    ""name"": ""TestDoc"",
    ""documentNamespace"": ""https://sbom.microsoft/test/1.0.0/abc"",
    ""creationInfo"": {
        ""created"": ""2024-01-01T00:00:00Z"",
        ""creators"": [
            ""Person: John Doe (john@example.com)""
        ]
    },
    ""documentDescribes"": [""SPDXRef-RootPackage""],
    ""packages"": [],
    ""relationships"": []
}";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("manifest.spdx.json", spdxFile)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var sbomComponent = (SpdxComponent)componentRecorder.GetDetectedComponents().Single().Component;
        sbomComponent.CreatorTool.Should().BeNull();
        sbomComponent.CreatorOrganization.Should().BeNull();
    }
}

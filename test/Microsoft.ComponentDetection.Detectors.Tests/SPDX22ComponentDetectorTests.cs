using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Spdx;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class SPDX22ComponentDetectorTests
    {
        private DetectorTestUtility<Spdx22ComponentDetector> detectorTestUtility;

        [TestInitialize]
        public void TestInitialize()
        {
            var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
            detectorTestUtility = DetectorTestUtilityCreator.Create<Spdx22ComponentDetector>()
                                    .WithScanRequest(new ScanRequest(new DirectoryInfo(Path.GetTempPath()), null, null, new Dictionary<string, string>(), null, componentRecorder));
        }

        [TestMethod]
        public async Task TestSbomDetector_SimpleSbom()
        {
            var spdxFile = @"{
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

            var (scanResult, componentRecorder) = await detectorTestUtility
                                                    .WithFile("manifest.spdx.json", spdxFile)
                                                    .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var components = detectedComponents.ToList();
            var sbomComponent = (SpdxComponent)components.FirstOrDefault()?.Component;

            if (sbomComponent is null)
            {
                throw new AssertFailedException($"{nameof(sbomComponent)} is null");
            }

            Assert.AreEqual(1, components.Count());
            Assert.AreEqual(sbomComponent.Name, "Test 1.0.0");
            Assert.AreEqual(sbomComponent.RootElementId, "SPDXRef-RootPackage");
            Assert.AreEqual(sbomComponent.DocumentNamespace,  new Uri("https://sbom.microsoft/Test/1.0.0/61de1a5-57cc-4732-9af5-edb321b4a7ee"));
        }

        [TestMethod]
        public async Task TestSbomDetector_BlankJson()
        {
            var spdxFile = "{}";
            
            var (scanResult, componentRecorder) = await detectorTestUtility
                .WithFile("manifest.spdx.json", spdxFile)
                .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var components = detectedComponents.ToList();
            Assert.IsFalse(components.Any());
        }

        [TestMethod]
        public async Task TestSbomDetector_InvalidFile()
        {
            var spdxFile = "invalidspdxfile";
            
            var (scanResult, componentRecorder) = await detectorTestUtility
                .WithFile("manifest.spdx.json", spdxFile)
                .ExecuteDetector();

            Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

            var detectedComponents = componentRecorder.GetDetectedComponents();
            var components = detectedComponents.ToList();
            Assert.IsFalse(components.Any()); 
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Spdx;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class Spdx30ComponentDetectorTests : BaseDetectorTest<Spdx30ComponentDetector>
{
    private const string DocumentId = "https://example.org/sbom/document";
    private const string RootId = "https://example.org/sbom/package";
    private const string Manifest = """
        {
          "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld",
          "@graph": [
            {
              "type": "SpdxDocument",
              "spdxId": "https://example.org/sbom/document",
              "name": "Example SBOM",
              "creationInfo": "_:creation",
              "rootElement": ["https://example.org/sbom/package"]
            },
            {
              "@id": "_:creation",
              "type": "CreationInfo",
              "specVersion": "3.0.1",
              "created": "2026-01-01T00:00:00Z",
              "createdBy": ["https://example.org/sbom/organization"],
              "createdUsing": ["https://example.org/sbom/tool"]
            },
            {
              "type": "Organization",
              "spdxId": "https://example.org/sbom/organization",
              "name": "Example Organization",
              "creationInfo": "_:creation"
            },
            {
              "type": "Tool",
              "spdxId": "https://example.org/sbom/tool",
              "name": "Example Tool",
              "creationInfo": "_:creation"
            },
            {
              "type": "software_Package",
              "spdxId": "https://example.org/sbom/package",
              "name": "Example Package",
              "creationInfo": "_:creation"
            }
          ]
        }
        """;

    [TestMethod]
    [DataRow("3.0.0")]
    [DataRow("3.0.1")]
    public async Task DetectsDocumentWithoutImportingPackagesAsync(string version)
    {
        const string fileName = "manifest.spdx.json";
        var content = Manifest.Replace("3.0.1", version, StringComparison.Ordinal);
        var (result, recorder) = await this.DetectorTestUtility
            .WithFile(fileName, content)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = recorder.GetDetectedComponents().Should().ContainSingle().Subject.Component
            .Should().BeOfType<SpdxComponent>().Subject;

#pragma warning disable CA5350, CA1308 // Match the lowercase SHA1 fingerprint required by existing SPDX reference consumers.
        var checksum = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
#pragma warning restore CA5350, CA1308

        component.Name.Should().Be("Example SBOM");
        component.SpdxVersion.Should().Be($"SPDX-{version}");
        component.DocumentNamespace.Should().Be(new Uri(DocumentId));
        component.RootElementId.Should().Be(RootId);
        component.Checksum.Should().Be(checksum);
        component.Path.Should().Be(Path.Combine(Path.GetTempPath(), fileName));
        component.CreatorTool.Should().Be("Example Tool");
        component.CreatorOrganization.Should().Be("Example Organization");
    }

    [TestMethod]
    public async Task ResolvesEmbeddedCreationInfoAndCreatorsAsync()
    {
        var json = JsonNode.Parse(Manifest)!;
        var graph = json["@graph"]!.AsArray();
        var creation = graph[1]!.DeepClone();
        creation["createdBy"] = new JsonArray(graph[2]!.DeepClone());
        creation["createdUsing"] = new JsonArray(graph[3]!.DeepClone());
        graph[0]!["creationInfo"] = creation;
        graph.RemoveAt(1);

        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("manifest.spdx.json", json.ToJsonString())
            .ExecuteDetectorAsync();

        var component = (SpdxComponent)recorder.GetDetectedComponents().Single().Component;
        component.CreatorTool.Should().Be("Example Tool");
        component.CreatorOrganization.Should().Be("Example Organization");
    }

    [TestMethod]
    [DataRow("[]", DocumentId)]
    [DataRow("[\"https://example.org/sbom/package\", \"https://example.org/sbom/other\"]", RootId)]
    [DataRow("\"https://example.org/sbom/package\"", RootId)]
    [DataRow("[{\"spdxId\":\"https://example.org/sbom/package\"}]", RootId)]
    public async Task SelectsRootAndUsesDocumentIdForUnnamedDocumentsAsync(string roots, string expectedRoot)
    {
        var json = JsonNode.Parse(Manifest)!;
        var document = json["@graph"]![0]!.AsObject();
        document.Remove("name");
        document["rootElement"] = JsonNode.Parse(roots);

        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("manifest.spdx.json", json.ToJsonString())
            .ExecuteDetectorAsync();

        var component = (SpdxComponent)recorder.GetDetectedComponents().Single().Component;
        component.Name.Should().Be(DocumentId);
        component.RootElementId.Should().Be(expectedRoot);
    }

    [TestMethod]
    public async Task UsesDocumentAsRootWhenRootElementIsAbsentAsync()
    {
        var json = JsonNode.Parse(Manifest)!;
        json["@graph"]![0]!.AsObject().Remove("rootElement");
        var creation = json["@graph"]![1]!.AsObject();
        creation.Remove("createdUsing");
        creation["createdBy"] = new JsonArray("https://example.org/unresolved-agent");

        var (_, recorder) = await this.DetectorTestUtility
            .WithFile("manifest.spdx.json", json.ToJsonString())
            .ExecuteDetectorAsync();

        var component = (SpdxComponent)recorder.GetDetectedComponents().Single().Component;
        component.RootElementId.Should().Be(DocumentId);
        component.CreatorTool.Should().BeNull();
        component.CreatorOrganization.Should().BeNull();
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"spdxVersion\":\"SPDX-2.2\"}")]
    [DataRow("{\"@graph\":{}}")]
    [DataRow("{\"@graph\":[]}")]
    [DataRow("{\"@graph\":[{\"type\":\"software_Package\"}]}")]
    public async Task SkipsNonSpdx30DocumentsWithWarningAsync(string content)
    {
        await this.AssertSkippedWithWarningAsync(content);
    }

    [TestMethod]
    [DataRow(0, "spdxId", "\"relative-document\"")]
    [DataRow(0, "spdxId", "null")]
    [DataRow(0, "creationInfo", "\"_:unresolved\"")]
    [DataRow(0, "rootElement", "[42]")]
    [DataRow(0, "name", "42")]
    [DataRow(1, "specVersion", "\"2.2\"")]
    [DataRow(1, "specVersion", "\"3.1.0\"")]
    [DataRow(1, "specVersion", "\"3.0.1-draft\"")]
    [DataRow(1, "specVersion", "null")]
    public async Task SkipsInvalidOrUnsupportedDocumentsWithWarningAsync(int index, string property, string value)
    {
        var json = JsonNode.Parse(Manifest)!;
        json["@graph"]![index]![property] = JsonNode.Parse(value);
        await this.AssertSkippedWithWarningAsync(json.ToJsonString());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task RejectsMultipleDocumentsOrDuplicateIdentifiersAsync(int index)
    {
        var json = JsonNode.Parse(Manifest)!;
        var graph = json["@graph"]!.AsArray();
        graph.Add(graph[index]!.DeepClone());
        await this.AssertSkippedWithWarningAsync(json.ToJsonString());
    }

    [TestMethod]
    public async Task PropagatesUnexpectedStreamErrorsAsync()
    {
        var stream = new Mock<Stream>();
        stream.SetupGet(s => s.CanRead).Returns(true);
        stream.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Read failed."));

        Func<Task> action = () => this.DetectorTestUtility
            .WithFile("manifest.spdx.json", stream.Object)
            .ExecuteDetectorAsync();

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Read failed.");
    }

    private async Task AssertSkippedWithWarningAsync(string content)
    {
        var logger = new Mock<ILogger<Spdx30ComponentDetector>>();
        var (result, recorder) = await this.DetectorTestUtility
            .AddServiceMock(logger)
            .WithFile("manifest.spdx.json", content)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        recorder.GetDetectedComponents().Should().BeEmpty();
        logger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

namespace Microsoft.ComponentDetection.Detectors.Spdx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class Spdx30ComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private static readonly Regex SupportedVersionRegex = new Regex(@"\A3\.0\.(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public Spdx30ComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<Spdx30ComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "SPDX30SBOM";

    public override IEnumerable<string> Categories => [nameof(DetectorClass.Spdx)];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Spdx];

    public override int Version => 1;

    public override IList<string> SearchPatterns => ["*.spdx.json"];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var file = processRequest.ComponentStream;
        this.Logger.LogDebug("Discovered SPDX 3.0 manifest file at: {ManifestLocation}", file.Location);

        try
        {
            // Keep the SHA1 document fingerprint used by SpdxComponent and existing SBOM reference consumers.
#pragma warning disable CA5350, CA1308 // Existing SPDX document references require lowercase SHA1 checksums.
            var checksum = Convert.ToHexString(await SHA1.HashDataAsync(file.Stream, cancellationToken)).ToLowerInvariant();
#pragma warning restore CA5350, CA1308
            file.Stream.Seek(0, SeekOrigin.Begin);

            using var json = await JsonDocument.ParseAsync(file.Stream, cancellationToken: cancellationToken);
            var component = this.ReadDocument(json.RootElement, checksum, file.Location);
            if (component != null)
            {
                processRequest.SingleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
            }
        }
        catch (JsonException e)
        {
            this.Logger.LogWarning(e, "Unable to parse SPDX 3.0 file at {ManifestLocation}, skipping", file.Location);
        }
        catch (IOException e)
        {
            this.Logger.LogError(e, "Error while reading SPDX 3.0 file at {ManifestLocation}", file.Location);
        }
        catch (Exception e) when (e is OutOfMemoryException or OverflowException)
        {
            this.Logger.LogWarning(e, "SPDX 3.0 file at {ManifestLocation} exceeds available memory or supported size, skipping", file.Location);
        }
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Expected a string for {property}.");
        }

        return value.GetString();
    }

    private static bool HasType(JsonElement element, string type)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("type", out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() == type
            : value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == type);
    }

    private static string? ReadId(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() : ReadString(element, "spdxId") ?? ReadString(element, "@id");

    private static JsonElement Resolve(JsonElement element, IDictionary<string, JsonElement> elements)
    {
        var id = ReadId(element);
        return id != null && elements.TryGetValue(id, out var resolved) ? resolved : element;
    }

    private static IEnumerable<JsonElement> ReadValues(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return [];
        }

        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [value];
    }

    private static string? ReadCreator(JsonElement creationInfo, string property, string type, IDictionary<string, JsonElement> elements) =>
        ReadValues(creationInfo, property)
            .Select(element => Resolve(element, elements))
            .Where(element => HasType(element, type))
            .Select(element => ReadString(element, "name"))
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

    private SpdxComponent? ReadDocument(JsonElement root, string checksum, string path)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("@graph", out var graph)
            || graph.ValueKind != JsonValueKind.Array)
        {
            this.Logger.LogDebug("Discovered file at {ManifestLocation} is not an SPDX 3.0 JSON-LD graph, skipping", path);
            return null;
        }

        var documents = graph.EnumerateArray().Where(element => HasType(element, "SpdxDocument")).ToList();
        if (documents.Count != 1)
        {
            this.Logger.LogWarning("SPDX 3.0 file at {ManifestLocation} must contain exactly one SpdxDocument, skipping", path);
            return null;
        }

        var elements = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var element in graph.EnumerateArray())
        {
            var id = ReadId(element);
            if (id != null && !elements.TryAdd(id, element))
            {
                throw new JsonException($"Duplicate SPDX element identifier: {id}.");
            }
        }

        var document = documents[0];
        if (!document.TryGetProperty("creationInfo", out var creationInfo))
        {
            throw new JsonException("SpdxDocument is missing creationInfo.");
        }

        creationInfo = Resolve(creationInfo, elements);
        var version = ReadString(creationInfo, "specVersion")
            ?? throw new JsonException("SpdxDocument creationInfo is missing specVersion.");

        if (!SupportedVersionRegex.IsMatch(version))
        {
            this.Logger.LogDebug("SPDX file at {ManifestLocation} has unsupported specVersion {SpecVersion}, skipping", path, version);
            return null;
        }

        var documentId = ReadId(document);
        if (string.IsNullOrWhiteSpace(documentId) || !Uri.TryCreate(documentId, UriKind.Absolute, out var documentUri))
        {
            throw new JsonException("SpdxDocument must have an absolute spdxId.");
        }

        var roots = ReadValues(document, "rootElement").Select(ReadId).ToList();
        if (roots.Any(id => string.IsNullOrWhiteSpace(id)))
        {
            throw new JsonException("SpdxDocument contains an invalid rootElement.");
        }

        if (roots.Count > 1)
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} has more than one rootElement, first will be selected as root element.", path);
        }
        else if (roots.Count == 0)
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} has no rootElement, considering the SpdxDocument as the root element.", path);
        }

        // SPDX 3 has no documentNamespace, and name is optional; the document IRI identifies the document.
        var name = ReadString(document, "name");
        return new SpdxComponent(
            $"SPDX-{version}",
            documentUri,
            string.IsNullOrWhiteSpace(name) ? documentId : name,
            checksum,
            roots.FirstOrDefault() ?? documentId,
            path)
        {
            CreatorTool = ReadCreator(creationInfo, "createdUsing", "Tool", elements),
            CreatorOrganization = ReadCreator(creationInfo, "createdBy", "Organization", elements),
        };
    }
}

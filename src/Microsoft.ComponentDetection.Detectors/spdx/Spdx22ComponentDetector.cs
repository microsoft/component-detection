#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Spdx;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Spdx22ComponentDetector discover SPDX SBOM files in JSON format and create components with the information about
/// what SPDX document describes.
/// </summary>
public class Spdx22ComponentDetector : SpdxComponentDetector, IDefaultOffComponentDetector
{
    private readonly IEnumerable<string> supportedSPDXVersions = ["SPDX-2.2"];

    public Spdx22ComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<Spdx22ComponentDetector> logger)
        : base(componentStreamEnumerableFactory, walkerFactory, logger)
    {
    }

    public override string Id => "SPDX22SBOM";

    public override int Version => 1;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        this.Logger.LogDebug("Discovered SPDX2.2 manifest file at: {ManifestLocation}", processRequest.ComponentStream.Location);

        try
        {
            await this.ProcessFileAsync(processRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            this.Logger.LogWarning("Unable to parse file at {ManifestLocation}, skipping", processRequest.ComponentStream.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Error while processing SPDX file at {ManifestLocation}", processRequest.ComponentStream.Location);
        }
    }

    protected override SpdxComponent ReadDocument(JsonElement root, string checksum, string path)
    {
        if (this.IsSPDXVersionSupported(root))
        {
            return this.ConvertJsonElementToSbomComponent(root, checksum, path);
        }

        this.Logger.LogWarning("Discovered SPDX at {ManifestLocation} is not SPDX-2.2 document, skipping", path);
        return null;
    }

    private bool IsSPDXVersionSupported(JsonElement document)
    {
        if (document.TryGetProperty("spdxVersion", out var versionElement))
        {
            var version = versionElement.GetString();
            return this.supportedSPDXVersions.Contains(version, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }

    private SpdxComponent ConvertJsonElementToSbomComponent(JsonElement document, string fileHash, string path)
    {
        var sbomNamespace = document.TryGetProperty("documentNamespace", out var nsElement) ? nsElement.GetString() : null;
        var name = document.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var spdxVersion = document.TryGetProperty("spdxVersion", out var spdxVersionElement) ? spdxVersionElement.GetString() : null;

        string[] rootElements = null;
        if (document.TryGetProperty("documentDescribes", out var describesElement) && describesElement.ValueKind == JsonValueKind.Array)
        {
            rootElements = describesElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => s != null)
                .ToArray();
        }

        if (rootElements is { Length: > 1 })
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} has more than one element in documentDescribes, first will be selected as root element.", path);
        }

        if (rootElements is { Length: 0 })
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} does not have root elements in documentDescribes section, considering SPDXRef-Document as a root element.", path);
        }

        var rootElementId = rootElements?.FirstOrDefault() ?? "SPDXRef-Document";
        var component = new SpdxComponent(spdxVersion, new Uri(sbomNamespace), name, fileHash, rootElementId, path);

        if (document.TryGetProperty("creationInfo", out var creationInfoElement)
            && creationInfoElement.TryGetProperty("creators", out var creatorsElement)
            && creatorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var creator in creatorsElement.EnumerateArray())
            {
                if (creator.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var creatorString = creator.GetString();
                if (creatorString == null)
                {
                    continue;
                }

                if (component.CreatorTool == null && creatorString.StartsWith("Tool: ", StringComparison.Ordinal))
                {
                    var tool = creatorString["Tool: ".Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(tool))
                    {
                        component.CreatorTool = tool;
                    }
                }
                else if (component.CreatorOrganization == null && creatorString.StartsWith("Organization: ", StringComparison.Ordinal))
                {
                    var org = creatorString["Organization: ".Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(org))
                    {
                        component.CreatorOrganization = org;
                    }
                }
            }
        }

        return component;
    }
}

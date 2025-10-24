#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Spdx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Spdx22ComponentDetector discover SPDX SBOM files in JSON format and create components with the information about
/// what SPDX document describes.
/// </summary>
public class Spdx22ComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private readonly IEnumerable<string> supportedSPDXVersions = ["SPDX-2.2"];

    public Spdx22ComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<Spdx22ComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.Spdx)];

    public override string Id => "SPDX22SBOM";

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Spdx];

    public override int Version => 1;

    public override IList<string> SearchPatterns => ["*.spdx.json"];

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        this.Logger.LogDebug("Discovered SPDX2.2 manifest file at: {ManifestLocation}", processRequest.ComponentStream.Location);
        var file = processRequest.ComponentStream;

        try
        {
            var hash = this.GetSHA1HashFromStream(file.Stream);

            // Reset buffer to starting position after hash generation.
            file.Stream.Seek(0, SeekOrigin.Begin);

            using var sr = new StreamReader(file.Stream);
            using var reader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();

            try
            {
                var document = serializer.Deserialize<JObject>(reader);
                if (document != null)
                {
                    if (this.IsSPDXVersionSupported(document))
                    {
                        var sbomComponent = this.ConvertJObjectToSbomComponent(processRequest, document, hash);
                        processRequest.SingleFileComponentRecorder.RegisterUsage(new DetectedComponent(sbomComponent));
                    }
                    else
                    {
                        this.Logger.LogWarning("Discovered SPDX at {ManifestLocation} is not SPDX-2.2 document, skipping", processRequest.ComponentStream.Location);
                    }
                }
                else
                {
                    this.Logger.LogWarning("Discovered SPDX file at {ManifestLocation} is not a valid document, skipping", processRequest.ComponentStream.Location);
                }
            }
            catch (JsonReaderException)
            {
                this.Logger.LogWarning("Unable to parse file at {ManifestLocation}, skipping", processRequest.ComponentStream.Location);
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Error while processing SPDX file at {ManifestLocation}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    private bool IsSPDXVersionSupported(JObject document) => this.supportedSPDXVersions.Contains(document["spdxVersion"]?.ToString(), StringComparer.OrdinalIgnoreCase);

    private SpdxComponent ConvertJObjectToSbomComponent(ProcessRequest processRequest, JObject document, string fileHash)
    {
        var sbomNamespace = document["documentNamespace"]?.ToString();
        var rootElements = document["documentDescribes"]?.ToObject<string[]>();
        var name = document["name"]?.ToString();
        var spdxVersion = document["spdxVersion"]?.ToString();

        if (rootElements?.Length > 1)
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} has more than one element in documentDescribes, first will be selected as root element.", processRequest.ComponentStream.Location);
        }

        if (rootElements != null && rootElements.Length == 0)
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} does not have root elements in documentDescribes section, considering SPDXRef-Document as a root element.", processRequest.ComponentStream.Location);
        }

        var rootElementId = rootElements?.FirstOrDefault() ?? "SPDXRef-Document";
        var path = processRequest.ComponentStream.Location;
        var component = new SpdxComponent(spdxVersion, new Uri(sbomNamespace), name, fileHash, rootElementId, path);

        return component;
    }

    private string GetSHA1HashFromStream(Stream stream)
    {
#pragma warning disable CA5350 // Suppress Do Not Use Weak Cryptographic Algorithms because we use SHA1 intentionally in SPDX format
        return BitConverter.ToString(SHA1.Create().ComputeHash(stream)).Replace("-", string.Empty).ToLower(); // CodeQL [SM02196] Sha1 is used in SPDX 2.2 format this file is parsing (https://spdx.github.io/spdx-spec/v2.2.2/file-information/).
#pragma warning restore CA5350
    }
}

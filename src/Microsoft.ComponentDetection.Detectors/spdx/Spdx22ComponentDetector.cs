namespace Microsoft.ComponentDetection.Detectors.Spdx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Spdx.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

/// <summary>
/// Spdx22ComponentDetector discover SPDX SBOM files in JSON format and create components with the information about
/// what SPDX document describes.
/// </summary>
public class Spdx22ComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private readonly IEnumerable<string> supportedSPDXVersions = new List<string> { "SPDX-2.2" };

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
        new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Spdx) };

    public override string Id => "SPDX22SBOM";

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Spdx };

    public override int Version => 1;

    public override IList<string> SearchPatterns => new List<string> { "*.spdx.json" };

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        this.Logger.LogDebug("Discovered SPDX2.2 manifest file at: {ManifestLocation}", processRequest.ComponentStream.Location);
        var file = processRequest.ComponentStream;
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;

        try
        {
            var hash = this.GetSHA1HashFromStream(file.Stream);

            // Reset buffer to starting position after hash generation.
            file.Stream.Seek(0, SeekOrigin.Begin);

            using var sr = new StreamReader(file.Stream);
            using var reader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();

            var spdxFileData = serializer.Deserialize<SpdxFileData>(reader);

            if (spdxFileData == null)
            {
                this.Logger.LogWarning("Discovered SPDX file at {ManifestLocation} is not a valid document, skipping", processRequest.ComponentStream.Location);
                return Task.CompletedTask;
            }

            if (!this.IsSPDXVersionSupported(spdxFileData.Version))
            {
                this.Logger.LogWarning("Discovered SPDX at {ManifestLocation} is not SPDX-2.2 document, skipping", processRequest.ComponentStream.Location);
                return Task.CompletedTask;
            }

            var sbomComponent = this.ConvertJObjectToSbomComponent(processRequest, spdxFileData, hash);
            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(sbomComponent));

            if (spdxFileData.HasPackages())
            {
                foreach (var package in spdxFileData.Packages)
                {
                    var spdxPackageComponent = new SpdxPackageComponent(package.Name, package.Version, package.Supplier, processRequest.ComponentStream.Location, package.CopyrightText);
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(spdxPackageComponent));
                }
            }
        }
        catch (JsonException je)
        {
            this.Logger.LogWarning(je, "Unable to parse file at {ManifestLocation}, skipping", processRequest.ComponentStream.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Error while processing SPDX file at {ManifestLocation}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    private bool IsSPDXVersionSupported(string version) => this.supportedSPDXVersions.Contains(version?.ToString(), StringComparer.OrdinalIgnoreCase);

    private SpdxComponent ConvertJObjectToSbomComponent(ProcessRequest processRequest, SpdxFileData spdxFileData, string fileHash)
    {
        var rootElements = spdxFileData.DocumentDescribes;

        if (rootElements?.Count() > 1)
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} has more than one element in documentDescribes, first will be selected as root element.", processRequest.ComponentStream.Location);
        }

        if (rootElements != null && !rootElements.Any())
        {
            this.Logger.LogWarning("SPDX file at {ManifestLocation} does not have root elements in documentDescribes section, considering SPDXRef-Document as a root element.", processRequest.ComponentStream.Location);
        }

        var rootElementId = rootElements?.FirstOrDefault() ?? "SPDXRef-Document";
        var path = processRequest.ComponentStream.Location;
        var component = new SpdxComponent(spdxFileData.Version, new Uri(spdxFileData.DocumentNamespace), spdxFileData.Name, fileHash, rootElementId, path);

        return component;
    }

    private string GetSHA1HashFromStream(Stream stream)
    {
#pragma warning disable CA5350 // Suppress Do Not Use Weak Cryptographic Algorithms because we use SHA1 intentionally in SPDX format
        return BitConverter.ToString(SHA1.Create().ComputeHash(stream)).Replace("-", string.Empty).ToLower();
#pragma warning restore CA5350
    }
}

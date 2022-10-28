using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.ComponentDetection.Detectors.Spdx
{
    /// <summary>
    /// Spdx22ComponentDetector discover SPDX SBOM files in JSON format and create components with the information about
    /// what SPDX document describes.
    /// </summary>
    [Export(typeof(IComponentDetector))]
    public class Spdx22ComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
    {
        private readonly IEnumerable<string> supportedSPDXVersions = new List<string> { "SPDX-2.2" };

        public override IEnumerable<string> Categories =>
            new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Spdx) };

        public override string Id => "SPDX22SBOM";

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Spdx };

        public override int Version => 1;

        public override IList<string> SearchPatterns { get; } = new List<string> { "*.spdx.json" };

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            this.Logger.LogVerbose($"Discovered SPDX2.2 manifest file at: {processRequest.ComponentStream.Location}");
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
                            this.Logger.LogWarning($"Discovered SPDX at {processRequest.ComponentStream.Location} is not SPDX-2.2 document, skipping");
                        }
                    }
                    else
                    {
                        this.Logger.LogWarning($"Discovered SPDX file at {processRequest.ComponentStream.Location} is not a valid document, skipping");
                    }
                }
                catch (JsonReaderException)
                {
                    this.Logger.LogWarning($"Unable to parse file at {processRequest.ComponentStream.Location}, skipping");
                }
            }
            catch (Exception e)
            {
                this.Logger.LogFailedReadingFile(file.Location, e);
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

            if (rootElements == null || rootElements.Length <= 1)
            {
            }
            else
            {
                this.Logger.LogWarning($"SPDX file at {processRequest.ComponentStream.Location} has more than one element in documentDescribes, first will be selected as root element.");
            }

            if (rootElements != null && rootElements.Any())
            {
                this.Logger.LogWarning($"SPDX file at {processRequest.ComponentStream.Location} does not have root elements in documentDescribes section, considering SPDXRef-Document as a root element.");
            }

            var rootElementId = rootElements?.FirstOrDefault() ?? "SPDXRef-Document";
            var path = processRequest.ComponentStream.Location;
            var component = new SpdxComponent(spdxVersion, new Uri(sbomNamespace), name, fileHash, rootElementId, path);

            return component;
        }

        private string GetSHA1HashFromStream(Stream stream)
        {
#pragma warning disable CA5350 // Suppress Do Not Use Weak Cryptographic Algorithms because we use SHA1 intentionally in SPDX format
            return BitConverter.ToString(SHA1.Create().ComputeHash(stream)).Replace("-", string.Empty).ToLower();
#pragma warning restore CA5350
        }
    }
}

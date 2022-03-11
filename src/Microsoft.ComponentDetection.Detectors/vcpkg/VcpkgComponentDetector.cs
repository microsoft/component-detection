using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Detectors.Vcpkg
{
    [Export(typeof(IComponentDetector))]
    public class VcpkgComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
    {
        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        [Import]
        public IEnvironmentVariableService EnvVarService { get; set; }

        public override string Id { get; } = "Vcpkg";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Vcpkg) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "vcpkg.spdx.json" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Vcpkg };

        public override int Version => 1;

        private HashSet<string> projectRoots = new HashSet<string>();

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            Logger.LogWarning($"vcpkg detector found {file}");

            var projectRootDirectory = Directory.GetParent(file.Location);
            if (projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
            {
                return;
            }

            await ParseSpdxFile(singleFileComponentRecorder, file);
        }

        private async Task ParseSpdxFile(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            IComponentStream file)
        {
            using var reader = new StreamReader(file.Stream);
            var sbom = JsonConvert.DeserializeObject<VcpkgSBOM>(await reader.ReadToEndAsync());

            foreach (var item in sbom.Packages)
            {
                if (string.IsNullOrEmpty(item.Name))
                {
                    continue;
                }

                try
                {
                    Logger.LogWarning($"parsed package {item.Name}");
                    if (item.SPDXID == "SPDXRef-port")
                    {
                        var split = item.VersionInfo.Split('#');
                        var component = new VcpkgComponent(item.SPDXID, item.Name, split[0], portVersion: split.Length >= 2 ? split[1] : "0", downloadLocation: item.DownloadLocation);
                        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                    }
                    else if (item.SPDXID == "SPDXRef-binary")
                    {
                        var split = item.Name.Split(':');
                        var component = new VcpkgComponent(item.SPDXID, item.Name, item.VersionInfo, triplet: split[1], downloadLocation: item.DownloadLocation);
                        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                    }
                    else if (item.SPDXID.StartsWith("SPDXRef-resource-"))
                    {
                        var dl = item.DownloadLocation;
                        var split = dl.Split("#");
                        var subpath = split.Length > 1 ? split[1] : null;
                        dl = split.Length > 1 ? split[0] : dl;
                        split = dl.Split("@");
                        var version = split.Length > 1 ? split[1] : null;
                        dl = split.Length > 1 ? split[0] : dl;

                        var component = new VcpkgComponent(item.SPDXID, item.Name, version, downloadLocation: dl);
                        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                    }
                }
                catch (Exception)
                {
                    Logger.LogWarning($"failed while handling {item.Name}");
                }
            }
        }
    }
}

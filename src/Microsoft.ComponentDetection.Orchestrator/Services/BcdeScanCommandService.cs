using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts.ArgumentSets;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Mappers;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Orchestrator.Services
{
    [Export(typeof(IArgumentHandlingService))]
    public class BcdeScanCommandService : ServiceBase, IArgumentHandlingService
    {
        public const string ManifestRelativePath = "ScanManifest_{timestamp}.json";

        [Import]
        public IFileWritingService FileWritingService { get; set; }

        [Import]
        public IBcdeScanExecutionService BcdeScanExecutionService { get; set; }

        public bool CanHandle(IScanArguments arguments)
        {
            return arguments is BcdeArguments;
        }

        public async Task<ScanResult> Handle(IScanArguments arguments)
        {
            BcdeArguments bcdeArguments = (BcdeArguments)arguments;
            var result = await BcdeScanExecutionService.ExecuteScanAsync(bcdeArguments);
            WriteComponentManifest(bcdeArguments, result);
            return result;
        }

        private void WriteComponentManifest(IDetectionArguments detectionArguments, ScanResult scanResult)
        {
            FileInfo userRequestedManifestPath = null;

            if (detectionArguments.ManifestFile != null)
            {
                Logger.LogInfo($"Scan Manifest file: {detectionArguments.ManifestFile.FullName}");
                userRequestedManifestPath = detectionArguments.ManifestFile;
            }
            else
            {
                Logger.LogInfo($"Scan Manifest file: {FileWritingService.ResolveFilePath(ManifestRelativePath)}");
            }

            var outputText = detectionArguments.ManifestFileFormat switch
            {
                ManifestFileFormat.ComponentDetection => JsonConvert.SerializeObject(scanResult, Formatting.Indented),
                ManifestFileFormat.CycloneDx => scanResult.ToCycloneDxString(),
                ManifestFileFormat.Spdx => scanResult.ToSpdxString(),
                _ => null
            };

            if (userRequestedManifestPath == null)
            {
                FileWritingService.WriteFile(ManifestRelativePath, outputText);
            }
            else
            {
                FileWritingService.WriteFile(userRequestedManifestPath, outputText);
            }
        }
    }
}

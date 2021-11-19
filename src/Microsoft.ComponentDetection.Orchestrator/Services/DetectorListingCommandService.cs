using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

namespace Microsoft.ComponentDetection.Orchestrator.Services
{
    [Export(typeof(IArgumentHandlingService))]
    public class DetectorListingCommandService : ServiceBase, IArgumentHandlingService
    {
        [Import]
        public IDetectorRegistryService DetectorRegistryService { get; set; }

        public bool CanHandle(IScanArguments arguments)
        {
            return arguments is ListDetectionArgs;
        }

        public async Task<ScanResult> Handle(IScanArguments arguments)
        {
            await ListDetectorsAsync(arguments as IListDetectionArgs);
            return new ScanResult() 
            {
                ResultCode = ProcessingResultCode.Success,
            };
        }

        private async Task<ProcessingResultCode> ListDetectorsAsync(IScanArguments listArguments)
        {
            var detectors = DetectorRegistryService.GetDetectors(listArguments.AdditionalPluginDirectories, listArguments.AdditionalDITargets);
            if (detectors.Any())
            {
                foreach (var detector in detectors)
                {
                    Logger.LogInfo($"{detector.Id}");
                }
            }

            return await Task.FromResult(ProcessingResultCode.Success);
        }
    }
}

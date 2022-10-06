using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.OrchestratorNS.ArgumentSets;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services
{
    [Export(typeof(IArgumentHandlingService))]
    public class BcdeDevCommandService : ServiceBase, IArgumentHandlingService
    {
        [Import]
        public IBcdeScanExecutionService BcdeScanExecutionService { get; set; }

        public bool CanHandle(IScanArguments arguments)
        {
            return arguments is BcdeDevArguments;
        }

        public async Task<ScanResult> Handle(IScanArguments arguments)
        {
            // Run BCDE with the given arguments
            var detectionArguments = arguments as BcdeArguments;

            var result = await this.BcdeScanExecutionService.ExecuteScanAsync(detectionArguments);
            var detectedComponents = result.ComponentsFound.ToList();
            foreach (var detectedComponent in detectedComponents)
            {
                Console.WriteLine(detectedComponent.Component.Id);
            }

            // TODO: Get vulnerabilities from GH Advisories
            return result;
        }
    }
}

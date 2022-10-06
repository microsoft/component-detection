using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.OrchestratorNS.ArgumentSets;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services
{
    public interface IBcdeScanExecutionService
    {
        Task<ScanResult> ExecuteScanAsync(IDetectionArguments detectionArguments);
    }
}

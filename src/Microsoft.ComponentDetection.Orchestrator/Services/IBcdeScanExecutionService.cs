using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

namespace Microsoft.ComponentDetection.Orchestrator.Services;

public interface IBcdeScanExecutionService
{
    Task<ScanResult> ExecuteScanAsync(IDetectionArguments detectionArguments);
}

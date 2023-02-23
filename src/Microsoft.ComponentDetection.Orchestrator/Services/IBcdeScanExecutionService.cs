namespace Microsoft.ComponentDetection.Orchestrator.Services;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public interface IBcdeScanExecutionService
{
    Task<ScanResult> ExecuteScanAsync(IDetectionArguments detectionArguments);
}

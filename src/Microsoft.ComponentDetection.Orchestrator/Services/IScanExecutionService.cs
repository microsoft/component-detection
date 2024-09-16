namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.Commands;

public interface IScanExecutionService
{
    Task<ScanResult> ExecuteScanAsync(ScanSettings settings);
}

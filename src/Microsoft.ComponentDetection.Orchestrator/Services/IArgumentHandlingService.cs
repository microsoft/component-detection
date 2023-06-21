namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public interface IArgumentHandlingService
{
    bool CanHandle(IScanArguments arguments);

    Task<ScanResult> HandleAsync(IScanArguments arguments);
}

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.OrchestratorNS.ArgumentSets;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services
{
    public interface IArgumentHandlingService
    {
        bool CanHandle(IScanArguments arguments);

        Task<ScanResult> Handle(IScanArguments arguments);
    }
}

using System.Composition;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services
{
    public abstract class ServiceBase
    {
        [Import]
        public ILogger Logger { get; set; }
    }
}

namespace Microsoft.ComponentDetection.Orchestrator.Services
{
    using System.Composition;
    using Microsoft.ComponentDetection.Contracts;

    public abstract class ServiceBase
    {
        [Import]
        public ILogger Logger { get; set; }
    }
}

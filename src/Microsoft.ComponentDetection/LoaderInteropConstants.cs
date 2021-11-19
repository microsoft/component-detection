using Microsoft.ComponentDetection.Orchestrator;

namespace Microsoft.ComponentDetection.Loader
{
    public static class LoaderInteropConstants
    {
        public static readonly string OrchestratorAssemblyModule = typeof(Orchestrator.Orchestrator).Assembly.GetName().Name;
        public static readonly string OrchestratorTypeName = typeof(Orchestrator.Orchestrator).FullName;
    }
}

namespace Microsoft.ComponentDetection.Loader
{
    public static class LoaderInteropConstants
    {
        public static readonly string OrchestratorAssemblyModule = typeof(OrchestratorNS.Orchestrator).Assembly.GetName().Name;
        public static readonly string OrchestratorTypeName = typeof(OrchestratorNS.Orchestrator).FullName;
    }
}

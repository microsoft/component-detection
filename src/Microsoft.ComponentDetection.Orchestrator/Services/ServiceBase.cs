namespace Microsoft.ComponentDetection.Orchestrator.Services;

using Microsoft.Extensions.Logging;

public abstract class ServiceBase
{
    protected ILogger Logger { get; set; }
}

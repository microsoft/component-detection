namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using System;
using Serilog.Events;

public interface IScanArguments
{
    Guid CorrelationId { get; set; }

    LogEventLevel LogLevel { get; set; }

    int Timeout { get; set; }

    string Output { get; set; }
}

namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using System;
using Microsoft.ComponentDetection.Contracts;

public interface IScanArguments
{
    Guid CorrelationId { get; set; }

    VerbosityMode Verbosity { get; set; }

    int Timeout { get; set; }

    string Output { get; set; }
}

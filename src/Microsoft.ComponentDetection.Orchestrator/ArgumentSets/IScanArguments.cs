namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.ComponentDetection.Common;

    public interface IScanArguments
    {
        IEnumerable<DirectoryInfo> AdditionalPluginDirectories { get; set; }

        IEnumerable<string> AdditionalDITargets { get; set; }

        Guid CorrelationId { get; set; }

        VerbosityMode Verbosity { get; set; }

        int Timeout { get; set; }

        string Output { get; set; }
    }
}

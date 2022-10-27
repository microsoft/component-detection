using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Common;

namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets
{
    public interface IScanArguments
    {
        IEnumerable<DirectoryInfo> AdditionalPluginDirectories { get; set; }

        IEnumerable<string> AdditionalDITargets { get; set; }

        bool SkipPluginsDirectory { get; set; }

        Guid CorrelationId { get; set; }

        VerbosityMode Verbosity { get; set; }

        int Timeout { get; set; }

        string Output { get; set; }
    }
}

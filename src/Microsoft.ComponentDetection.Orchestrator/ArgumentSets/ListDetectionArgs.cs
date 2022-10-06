using System.Composition;
using CommandLine;

namespace Microsoft.ComponentDetection.OrchestratorNS.ArgumentSets
{
    [Verb("list-detectors", HelpText = "Lists available detectors")]
    [Export(typeof(IScanArguments))]
    public class ListDetectionArgs : BaseArguments, IListDetectionArgs
    {
    }
}

namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets
{
    using System.Composition;
    using CommandLine;

    [Verb("list-detectors", HelpText = "Lists available detectors")]
    [Export(typeof(IScanArguments))]
    public class ListDetectionArgs : BaseArguments, IListDetectionArgs
    {
    }
}

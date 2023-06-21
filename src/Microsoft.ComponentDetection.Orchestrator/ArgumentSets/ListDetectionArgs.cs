namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

using CommandLine;

[Verb("list-detectors", HelpText = "Lists available detectors")]
public class ListDetectionArgs : BaseArguments, IListDetectionArgs
{
}

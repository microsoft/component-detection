namespace Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using CommandLine;

[Verb("dev", HelpText = "Dev command", Hidden = true)]
public class BcdeDevArguments : BcdeArguments, IDetectionArguments
{
    // TODO: Add option to specify download directory for GH database
}

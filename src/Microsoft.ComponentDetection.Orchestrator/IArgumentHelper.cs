namespace Microsoft.ComponentDetection.Orchestrator;

using CommandLine;

internal interface IArgumentHelper
{
    ParserResult<object> ParseArguments(string[] args);

    ParserResult<T> ParseArguments<T>(string[] args, bool ignoreInvalidArgs = false);
}

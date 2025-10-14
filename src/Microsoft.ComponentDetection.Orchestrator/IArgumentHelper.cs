#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator;

using CommandLine;

public interface IArgumentHelper
{
    ParserResult<object> ParseArguments(string[] args);

    ParserResult<T> ParseArguments<T>(string[] args, bool ignoreInvalidArgs = false);
}

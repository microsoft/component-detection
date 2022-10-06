using CommandLine;

namespace Microsoft.ComponentDetection.Orchestrator
{
    public interface IArgumentHelper
    {
        ParserResult<object> ParseArguments(string[] args);
    }
}

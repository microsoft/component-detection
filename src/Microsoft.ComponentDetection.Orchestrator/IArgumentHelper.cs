using CommandLine;

namespace Microsoft.ComponentDetection.OrchestratorNS
{
    public interface IArgumentHelper
    {
        ParserResult<object> ParseArguments(string[] args);
    }
}

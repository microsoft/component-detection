namespace Microsoft.ComponentDetection.Orchestrator
{
    using CommandLine;

    public interface IArgumentHelper
    {
        ParserResult<object> ParseArguments(string[] args);
    }
}

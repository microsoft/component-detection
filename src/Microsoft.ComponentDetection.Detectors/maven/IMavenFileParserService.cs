namespace Microsoft.ComponentDetection.Detectors.Maven;

using Microsoft.ComponentDetection.Contracts.Internal;

public interface IMavenFileParserService
{
    void ParseDependenciesFile(ProcessRequest processRequest);
}

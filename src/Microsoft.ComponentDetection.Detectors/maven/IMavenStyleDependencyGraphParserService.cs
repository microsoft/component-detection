using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Detectors.Maven;

public interface IMavenStyleDependencyGraphParserService
{
    void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder);
}

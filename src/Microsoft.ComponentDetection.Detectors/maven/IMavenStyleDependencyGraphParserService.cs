#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using Microsoft.ComponentDetection.Contracts;

public interface IMavenStyleDependencyGraphParserService
{
    void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder);
}

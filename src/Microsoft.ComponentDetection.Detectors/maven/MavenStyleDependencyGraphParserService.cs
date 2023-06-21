namespace Microsoft.ComponentDetection.Detectors.Maven;

using Microsoft.ComponentDetection.Contracts;

public class MavenStyleDependencyGraphParserService : IMavenStyleDependencyGraphParserService
{
    public void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var parser = new MavenStyleDependencyGraphParser();
        parser.Parse(lines, singleFileComponentRecorder);
    }
}

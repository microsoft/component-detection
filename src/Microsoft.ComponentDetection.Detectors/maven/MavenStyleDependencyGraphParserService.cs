using Microsoft.ComponentDetection.Contracts;
using System.Composition;

namespace Microsoft.ComponentDetection.Detectors.Maven
{
    [Export(typeof(IMavenStyleDependencyGraphParserService))]
    public class MavenStyleDependencyGraphParserService : IMavenStyleDependencyGraphParserService
    {
        public void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            var parser = new MavenStyleDependencyGraphParser();
            parser.Parse(lines, singleFileComponentRecorder);
        }
    }
}

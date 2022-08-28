namespace Microsoft.ComponentDetection.Detectors.Maven
{
    using System.Composition;
    using Microsoft.ComponentDetection.Contracts;

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

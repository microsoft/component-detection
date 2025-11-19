#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class MavenStyleDependencyGraphParserService : IMavenStyleDependencyGraphParserService
{
    private readonly ILogger logger;

    public MavenStyleDependencyGraphParserService(ILogger<MavenStyleDependencyGraphParserService> logger) => this.logger = logger;

    public void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var parser = new MavenStyleDependencyGraphParser();
        parser.Parse(lines, singleFileComponentRecorder, this.logger);
    }
}

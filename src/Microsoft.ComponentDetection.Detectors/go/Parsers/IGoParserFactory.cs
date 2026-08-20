#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using Microsoft.Extensions.Logging;

public interface IGoParserFactory
{
    IGoParser CreateParser(GoParserType type, ILogger logger);
}

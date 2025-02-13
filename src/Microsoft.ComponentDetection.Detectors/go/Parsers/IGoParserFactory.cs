namespace Microsoft.ComponentDetection.Detectors.Go;

public interface IGoParserFactory
{
    IGoParser CreateParser(GoParserType type);
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.Extensions.Logging;

public interface IYarnLockParser
{
    bool CanParse(YarnLockVersion yarnLockVersion);

    YarnLockFile Parse(ISingleFileComponentRecorder singleFileComponentRecorder, IYarnBlockFile fileLines, ILogger logger);
}

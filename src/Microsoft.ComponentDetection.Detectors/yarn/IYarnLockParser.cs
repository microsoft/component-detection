using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

namespace Microsoft.ComponentDetection.Detectors.Yarn
{
    public interface IYarnLockParser
    {
        bool CanParse(YarnLockVersion yarnLockVersion);

        YarnLockFile Parse(IYarnBlockFile fileLines, ILogger logger);
    }
}

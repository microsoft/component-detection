using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers
{
    public interface IYarnBlockFile : IEnumerable<YarnBlock>
    {
        YarnLockVersion YarnLockVersion { get; set; }
    }
}

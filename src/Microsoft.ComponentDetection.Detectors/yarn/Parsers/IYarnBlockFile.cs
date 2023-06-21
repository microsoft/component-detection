namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

using System.Collections.Generic;

public interface IYarnBlockFile : IEnumerable<YarnBlock>
{
    YarnLockVersion YarnLockVersion { get; set; }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.Extensions.Logging;

public class YarnLockFileFactory : IYarnLockFileFactory
{
    private readonly IEnumerable<IYarnLockParser> parsers;

    public YarnLockFileFactory(IEnumerable<IYarnLockParser> parsers) => this.parsers = parsers;

    public async Task<YarnLockFile> ParseYarnLockFileAsync(ISingleFileComponentRecorder singleFileComponentRecorder, Stream file, ILogger logger)
    {
        var blockFile = await YarnBlockFile.CreateBlockFileAsync(file);

        foreach (var parser in this.parsers)
        {
            if (parser.CanParse(blockFile.YarnLockVersion))
            {
                return parser.Parse(singleFileComponentRecorder, blockFile, logger);
            }
        }

        throw new InvalidYarnLockFileException();
    }
}

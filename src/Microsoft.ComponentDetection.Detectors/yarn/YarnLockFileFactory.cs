using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

namespace Microsoft.ComponentDetection.Detectors.Yarn
{
    public static class YarnLockFileFactory
    {
        public static IList<IYarnLockParser> Parsers { get; }

        static YarnLockFileFactory()
        {
            Parsers = new List<IYarnLockParser> { new YarnLockParser() };
        }

        public static async Task<YarnLockFile> ParseYarnLockFileAsync(Stream file, ILogger logger)
        {
            var blockFile = await YarnBlockFile.CreateBlockFileAsync(file);

            foreach (var parser in Parsers)
            {
                if (parser.CanParse(blockFile.YarnLockVersion))
                {
                    return parser.Parse(blockFile, logger);
                }
            }

            throw new InvalidYarnLockFileException();
        }
    }
}

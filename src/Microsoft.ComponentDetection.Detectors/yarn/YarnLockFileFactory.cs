﻿namespace Microsoft.ComponentDetection.Detectors.Yarn;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

public static class YarnLockFileFactory
{
    static YarnLockFileFactory() => Parsers = new List<IYarnLockParser> { new YarnLockParser() };

    public static IList<IYarnLockParser> Parsers { get; }

    public static async Task<YarnLockFile> ParseYarnLockFileAsync(ISingleFileComponentRecorder singleFileComponentRecorder, Stream file, ILogger logger)
    {
        var blockFile = await YarnBlockFile.CreateBlockFileAsync(file);

        foreach (var parser in Parsers)
        {
            if (parser.CanParse(blockFile.YarnLockVersion))
            {
                return parser.Parse(singleFileComponentRecorder, blockFile, logger);
            }
        }

        throw new InvalidYarnLockFileException();
    }
}

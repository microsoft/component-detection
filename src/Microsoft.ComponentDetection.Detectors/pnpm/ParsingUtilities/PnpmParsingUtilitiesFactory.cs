#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.IO;
using YamlDotNet.Serialization;

public static class PnpmParsingUtilitiesFactory
{
    public static PnpmParsingUtilitiesBase<T> Create<T>()
    where T : PnpmYaml
    {
        return typeof(T).Name switch
        {
            nameof(PnpmYamlV5) => new PnpmV5ParsingUtilities<T>(),
            nameof(PnpmYamlV6) => new PnpmV6ParsingUtilities<T>(),
            nameof(PnpmYamlV9) => new PnpmV9ParsingUtilities<T>(),
            _ => new PnpmV5ParsingUtilities<T>(),
        };
    }

    public static string DeserializePnpmYamlFileVersion(string fileContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<PnpmYaml>(new StringReader(fileContent))?.LockfileVersion;
    }
}

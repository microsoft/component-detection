#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

internal static class PnpmParsingUtilitiesFactory
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

        var reader = new StringReader(fileContent);
        var parser = new Parser(reader);
        parser.Consume<StreamStart>();

        var versions = new List<string>();
        while (parser.TryConsume<DocumentStart>(out _))
        {
            var doc = deserializer.Deserialize<PnpmYaml>(parser);
            if (doc != null && !string.IsNullOrWhiteSpace(doc.LockfileVersion))
            {
                versions.Add(doc.LockfileVersion);
            }

            parser.TryConsume<DocumentEnd>(out _);
        }

        var distinctVersions = versions.Distinct().ToList();
        if (distinctVersions.Count > 1)
        {
            throw new InvalidOperationException($"Inconsistent lockfile versions found: {string.Join(", ", distinctVersions)}");
        }

        return distinctVersions.FirstOrDefault();
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using System.Linq;

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
        var documents = Create<PnpmYaml>().DeserializePnpmYamlFileDocuments(fileContent);
        var distinctVersions = documents
            .Select(doc => doc.LockfileVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct()
            .ToList();
        if (distinctVersions.Count > 1)
        {
            throw new InvalidOperationException($"Inconsistent lockfile versions found: {string.Join(", ", distinctVersions)}");
        }

        return distinctVersions.FirstOrDefault();
    }
}

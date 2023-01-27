namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

public class YarnLockParser : IYarnLockParser
{
    private const string VersionString = "version";

    private const string Resolved = "resolved";

    private const string Dependencies = "dependencies";

    private const string OptionalDependencies = "optionalDependencies";

    private static readonly List<YarnLockVersion> SupportedVersions = new List<YarnLockVersion> { YarnLockVersion.V1, YarnLockVersion.V2 };

    [Import]
    public ILogger Logger { get; set; }

    public static string NormalizeVersion(string version) => version.StartsWith("npm:") ? version : $"npm:{version}";

    public bool CanParse(YarnLockVersion yarnLockVersion) => SupportedVersions.Contains(yarnLockVersion);

    public YarnLockFile Parse(IYarnBlockFile blockFile, ILogger logger)
    {
        if (blockFile == null)
        {
            throw new ArgumentNullException(nameof(blockFile));
        }

        var file = new YarnLockFile { LockVersion = blockFile.YarnLockVersion };
        IList<YarnEntry> entries = new List<YarnEntry>();

        foreach (var block in blockFile)
        {
            var yarnEntry = new YarnEntry();
            var satisfiedPackages = block.Title.Split(',').Select(x => x.Trim())
                .Select(this.GenerateBlockTitleNormalizer(block));

            foreach (var package in satisfiedPackages)
            {
                if (!this.TryReadNameAndSatisfiedVersion(package, out var parsed))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(yarnEntry.Name))
                {
                    yarnEntry.Name = parsed.Item1;
                }

                yarnEntry.Satisfied.Add(NormalizeVersion(parsed.Item2));
            }

            if (string.IsNullOrWhiteSpace(yarnEntry.Name))
            {
                logger.LogWarning($"Failed to read a name for block {block.Title}. The entry will be skipped.");
                continue;
            }

            if (!block.Values.TryGetValue(VersionString, out var version))
            {
                logger.LogWarning($"Failed to read a version for {yarnEntry.Name}. The entry will be skipped.");
                continue;
            }

            yarnEntry.Version = version;

            if (block.Values.TryGetValue(Resolved, out var resolved))
            {
                yarnEntry.Resolved = resolved;
            }

            var dependencyBlock = block.Children.SingleOrDefault(x => string.Equals(x.Title, Dependencies, StringComparison.OrdinalIgnoreCase));

            if (dependencyBlock != null)
            {
                foreach (var item in dependencyBlock.Values)
                {
                    yarnEntry.Dependencies.Add(new YarnDependency { Name = item.Key, Version = NormalizeVersion(item.Value) });
                }
            }

            var optionalDependencyBlock = block.Children.SingleOrDefault(x => string.Equals(x.Title, OptionalDependencies, StringComparison.OrdinalIgnoreCase));

            if (optionalDependencyBlock != null)
            {
                foreach (var item in optionalDependencyBlock.Values)
                {
                    yarnEntry.OptionalDependencies.Add(new YarnDependency { Name = item.Key, Version = NormalizeVersion(item.Value) });
                }
            }

            entries.Add(yarnEntry);
        }

        file.Entries = entries;

        return file;
    }

    private Func<string, string> GenerateBlockTitleNormalizer(YarnBlock block) =>
        // For cases where we have no version in the title, ex:
        //   nyc:
        //    version "10.0.0"
        //    resolved "https://registry.Yarnpkg.com/nyc/-/nyc-10.0.0.tgz#95bd4a2c3487f33e1e78f213c6d5a53d88074ce6"
        blockTitleMember =>
        {
            if (blockTitleMember.Contains('@'))
            {
                return blockTitleMember;
            }

            var versionValue = block.Values.FirstOrDefault(x => string.Equals(x.Key, VersionString, StringComparison.OrdinalIgnoreCase));
            if (default(KeyValuePair<string, string>).Equals(versionValue))
            {
                this.Logger.LogWarning("Block without version detected");
                return blockTitleMember;
            }

            return blockTitleMember + $"@{versionValue.Value}";
        };

    private bool TryReadNameAndSatisfiedVersion(string nameVersionPairing, out Tuple<string, string> output)
    {
        output = null;
        var workingString = nameVersionPairing;
        workingString = workingString.TrimEnd(':');
        workingString = workingString.Trim('\"');
        var startsWithAtSign = false;
        if (workingString.StartsWith('@'))
        {
            startsWithAtSign = true;
            workingString = workingString.TrimStart('@');
        }

        var parts = workingString.Split('@');

        if (parts.Length != 2)
        {
            return false;
        }

        var at = startsWithAtSign ? "@" : string.Empty;
        var name = $"{at}{parts[0]}";

        output = new Tuple<string, string>(name, parts[1]);
        return true;
    }
}

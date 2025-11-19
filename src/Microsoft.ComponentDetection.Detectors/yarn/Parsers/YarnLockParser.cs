#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class YarnLockParser : IYarnLockParser
{
    private const string VersionString = "version";

    private const string Resolved = "resolved";

    private const string Resolution = "resolution";

    private const string Dependencies = "dependencies";

    private const string OptionalDependencies = "optionalDependencies";

    private static readonly List<YarnLockVersion> SupportedVersions = [YarnLockVersion.V1, YarnLockVersion.Berry];

    private readonly ILogger<YarnLockParser> logger;

    public YarnLockParser(ILogger<YarnLockParser> logger) => this.logger = logger;

    public static string NormalizeVersion(string version)
    {
        return version.StartsWith("npm:") ? version : $"npm:{version}";
    }

    public bool CanParse(YarnLockVersion yarnLockVersion)
    {
        return SupportedVersions.Contains(yarnLockVersion);
    }

    public YarnLockFile Parse(ISingleFileComponentRecorder singleFileComponentRecorder, IYarnBlockFile fileLines, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(fileLines);

        var file = new YarnLockFile { LockVersion = fileLines.YarnLockVersion, LockfileVersion = fileLines.LockfileVersion };
        IList<YarnEntry> entries = [];

        foreach (var block in fileLines)
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
                logger.LogWarning("Failed to read a name for block {BlockTitle}. The entry will be skipped.", block.Title);
                continue;
            }

            if (!block.Values.TryGetValue(VersionString, out var version))
            {
                logger.LogWarning("Failed to read a version for {YarnEntryName}. The entry will be skipped.", yarnEntry.Name);
                singleFileComponentRecorder.RegisterPackageParseFailure(yarnEntry.Name);
                continue;
            }

            yarnEntry.Version = version;

            if (block.Values.TryGetValue(Resolved, out var resolved))
            {
                yarnEntry.Resolved = resolved;
            }

            // Yarn berry renamed the "resolved" field to "resolution"
            else if (block.Values.TryGetValue(Resolution, out var resolution))
            {
                yarnEntry.Resolved = resolution;

                if (resolution.StartsWith(yarnEntry.Name + "@workspace:"))
                {
                    // Don't try to parse local workspace entries, which were never a part of the v1 lockfile
                    continue;
                }
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

    private Func<string, string> GenerateBlockTitleNormalizer(YarnBlock block)
    {
        // For cases where we have no version in the title, ex:
        //   nyc:
        //    version "10.0.0"
        //    resolved "https://registry.Yarnpkg.com/nyc/-/nyc-10.0.0.tgz#95bd4a2c3487f33e1e78f213c6d5a53d88074ce6"
        return blockTitleMember =>
        {
            if (blockTitleMember.Contains('@'))
            {
                return blockTitleMember;
            }

            var versionValue = block.Values.FirstOrDefault(x => string.Equals(x.Key, VersionString, StringComparison.OrdinalIgnoreCase));
            if (default(KeyValuePair<string, string>).Equals(versionValue))
            {
                this.logger.LogWarning("Block without version detected");
                return blockTitleMember;
            }

            return blockTitleMember + $"@{versionValue.Value}";
        };
    }

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

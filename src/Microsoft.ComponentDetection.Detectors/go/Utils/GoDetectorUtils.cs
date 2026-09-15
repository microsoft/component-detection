#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.ComponentDetection.Common;
using Microsoft.Extensions.Logging;

public static class GoDetectorUtils
{
    /// <summary>
    /// Evaluates if the provided go.sum file path should be scanned or not.
    /// </summary>
    /// <param name="goSumFilePath">Path to go.sum file that we are evaluating wether it should be scanned.</param>
    /// <param name="adjacentGoModFile">Component stream representing the adjacent go.mod file.</param>
    /// <param name="logger">The logger to use for logging messages.</param>
    /// <returns>True if the adjacent go.mod file is present and has a go version >= 1.17.</returns>
    public static bool ShouldIncludeGoSumFromDetection(string goSumFilePath, ComponentStream adjacentGoModFile, ILogger logger)
    {
        using var reader = new StreamReader(adjacentGoModFile.Stream);
        var goModFileContents = reader.ReadToEnd();

        var goVersionMatch = Regex.Match(goModFileContents, @"go\s(?<version>\d+\.\d+)");

        if (!goVersionMatch.Success)
        {
            logger.LogDebug(
                "go.sum file found with an adjacent go.mod file that does not contain a go version. Location: {Location}",
                goSumFilePath);
            return true;
        }

        var goVersion = goVersionMatch.Groups["version"].Value;
        if (Version.TryParse(goVersion, out var version))
        {
            if (version < new Version(1, 17))
            {
                logger.LogWarning(
                    "go.mod file at {GoModLocation} does not have a go version >= 1.17. Scanning this go.sum file: {GoSumLocation} which may lead to over reporting components",
                    adjacentGoModFile.Location,
                    goSumFilePath);

                return true;
            }

            logger.LogInformation(
                "go.sum file found with an adjacent go.mod file that has a go version >= 1.17. Will not scan this go.sum file. Location: {Location}",
                goSumFilePath);

            return false;
        }

        logger.LogWarning(
            "go.sum file found with an adjacent go.mod file that has an invalid go version. Scanning both for components. Location: {Location}",
            goSumFilePath);

        return true;
    }
}

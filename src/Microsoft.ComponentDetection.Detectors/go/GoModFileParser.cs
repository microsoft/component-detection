namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Utility class for parsing the go.mod file.
/// </summary>
internal sealed class GoModFileParser
{
    private const string RequireBlockStart = "require (";
    private const string GoStatement = "go ";
    private const string RequireStatement = "require ";

    /// <summary>
    /// Parses the go.mod file and registers the components found in it.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder.</param>
    /// <param name="file">The component stream of the go.mod file.</param>
    /// <param name="goGraphTelemetryRecord">Telemetry record to capture data.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task ParseGoModFileAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord goGraphTelemetryRecord,
        ILogger logger)
    {
        using var reader = new StreamReader(file.Stream);

        // There can be multiple require( ) sections in go 1.17+. loop over all of them.
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();

            while (line != null && !line.StartsWith(RequireBlockStart))
            {
                if (line.StartsWith(GoStatement))
                {
                    goGraphTelemetryRecord.GoModVersion = line[GoStatement.Length..].Trim();
                }

                // In go >= 1.17, direct dependencies are listed as "require x/y v1.2.3", and transitive dependencies
                // are listed in the require () section
                if (line.StartsWith(RequireStatement))
                {
                    TryRegisterDependencyFromModLine(line[RequireStatement.Length..], singleFileComponentRecorder, logger);
                }

                line = await reader.ReadLineAsync();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = await reader.ReadLineAsync()) != null && !line.EndsWith(")"))
            {
                TryRegisterDependencyFromModLine(line, singleFileComponentRecorder, logger);
            }
        }
    }

    private static void TryRegisterDependencyFromModLine(string line, ISingleFileComponentRecorder singleFileComponentRecorder, ILogger logger)
    {
        if (TryToCreateGoComponentFromModLine(line, out var goComponent))
        {
            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
        }
        else
        {
            var lineTrim = line.Trim();
            logger.LogWarning("Line could not be parsed for component [{LineTrim}]", lineTrim);
            singleFileComponentRecorder.RegisterPackageParseFailure(lineTrim);
        }
    }

    private static bool TryToCreateGoComponentFromModLine(string line, out GoComponent goComponent)
    {
        var lineComponents = Regex.Split(line.Trim(), @"\s+");

        if (lineComponents.Length < 2)
        {
            goComponent = null;
            return false;
        }

        var name = lineComponents[0];
        var version = lineComponents[1];
        goComponent = new GoComponent(name, version);

        return true;
    }

    /// <summary>
    /// Checks if we should parse the the go.sum file. We parse the go.sum file if the go.mod file does not contain a go
    /// version or if the go version is less than 1.17.
    /// </summary>
    /// <remarks>
    /// Prior to go 1.17, the go.mod file did not contain transitive dependencies, so we need to parse the go.sum file
    /// to get the full dependency graph. After go 1.17, the go.mod file contains transitive dependencies, and the
    /// go.sum file is only used for checksums.
    /// </remarks>
    /// <param name="goModFileContents">The contents of the go.mod file.</param>
    /// <param name="processRequest">The process requrest.</param>
    /// <param name="goModFile">the component stream of the goModFile.</param>
    /// <param name="logger">The logger.</param>
    /// <returns><c>true></c> if we must parse the go.sum file, <c>false</c> otherwise.</returns>
    public static bool ShouldParseGoSumFile(
        string goModFileContents,
        ProcessRequest processRequest,
        ComponentStream goModFile,
        ILogger logger)
    {
        var goVersionMatch = Regex.Match(goModFileContents, @"go\s(?<version>\d+\.\d+)");

        if (!goVersionMatch.Success)
        {
            logger.LogDebug(
                "go.sum file found with an adjacent go.mod file that does not contain a go version. Location: {Location}",
                processRequest.ComponentStream.Location);
            return true;
        }

        var goVersion = goVersionMatch.Groups["version"].Value;
        if (System.Version.TryParse(goVersion, out var version))
        {
            if (version < new Version(1, 17))
            {
                logger.LogWarning(
                    "go.mod file at {GoModLocation} does not have a go version >= 1.17. Scanning this go.sum " +
                    "file: {GoSumLocation} which may lead to over reporting components",
                    goModFile.Location,
                    processRequest.ComponentStream.Location);

                return true;
            }

            logger.LogInformation(
                "go.sum file found with an adjacent go.mod file that has a go version >= 1.17. Will not scan " +
                "this go.sum file. Location: {Location}",
                processRequest.ComponentStream.Location);

            return false;
        }

        logger.LogWarning(
            "go.sum file found with an adjacent go.mod file that has an invalid go version. Scanning both for " +
            "components. Location: {Location}",
            processRequest.ComponentStream.Location);

        return true;
    }
}

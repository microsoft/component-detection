#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class GoModParser : IGoParser
{
    private const string StartString = "require ";
    private readonly ILogger logger;

    public GoModParser(ILogger logger) => this.logger = logger;

    /// <summary>
    /// Checks whether the input path is a potential local file system path
    /// 1. '.' checks whether the path is relative to current directory.
    /// 2. '..' checks whether the path is relative to some ancestor directory.
    /// 3. IsRootedPath checks whether it is an absolute path.
    /// </summary>
    /// <param name="path">Candidate path.</param>
    /// <returns>true if potential local file system path.</returns>
    private static bool IsLocalPath(string path)
    {
        return path.StartsWith('.') || path.StartsWith("..") || Path.IsPathRooted(path);
    }

    /// <summary>
    /// Tries to extract source token from replace directive.
    /// </summary>
    /// <param name="directiveLine">String containing a directive after replace token.</param>
    /// <param name="replacePathDirectives">Hash set containing package+version? that are local references.</param>
    /// <param name="moduleReplaces">Dinctionary that maps source package+version? to replaced package+version?.</param>
    private static void HandleReplaceDirective(
            string directiveLine,
            HashSet<string> replacePathDirectives,
            Dictionary<string, GoReplaceDirective> moduleReplaces)
    {
        var parts = directiveLine.Split("=>", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return;
        }

        var sourceTokens = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sourceName = sourceTokens[0];
        var sourceVersion = sourceTokens.Length > 1 ? sourceTokens[1] : null;

        var targetTokens = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targetName = targetTokens[0];
        var targetVersion = targetTokens.Length > 1 ? targetTokens[1] : null;

        if (IsLocalPath(targetName))
        {
            var key = sourceVersion != null ? $"{sourceName}@{sourceVersion}" : sourceName;
            replacePathDirectives.Add(key);
        }
        else
        {
            var key = sourceVersion != null ? $"{sourceName}@{sourceVersion}" : sourceName;
            moduleReplaces[key] = new GoReplaceDirective(sourceName, sourceVersion, targetName, targetVersion);
        }
    }

    public async Task<bool> ParseAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord record)
    {
        // Collect replace directives
        var (replacePathDirectives, moduleReplacements) = await this.GetAllReplaceDirectivesAsync(file);

        // Rewind stream after reading replace directives
        file.Stream.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(file.Stream);

        // There can be multiple require( ) sections in go 1.17+. loop over all of them.
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();

            while (line != null && !line.StartsWith("require ("))
            {
                if (line.StartsWith("go "))
                {
                    record.GoModVersion = line[3..].Trim();
                }

                // In go >= 1.17, direct dependencies are listed as "require x/y v1.2.3", and transitive dependencies
                // are listed in the require () section
                if (line.StartsWith(StartString))
                {
                    this.TryRegisterDependencyFromModLine(file, line[StartString.Length..], singleFileComponentRecorder, replacePathDirectives, moduleReplacements);
                }

                line = await reader.ReadLineAsync();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = await reader.ReadLineAsync()) != null && !line.EndsWith(')'))
            {
                this.TryRegisterDependencyFromModLine(file, line, singleFileComponentRecorder, replacePathDirectives, moduleReplacements);
            }
        }

        return true;
    }

    private void TryRegisterDependencyFromModLine(IComponentStream file, string line, ISingleFileComponentRecorder singleFileComponentRecorder, HashSet<string> replacePathDirectives, Dictionary<string, GoReplaceDirective> moduleReplacements)
    {
        if (line.Trim().StartsWith("//"))
        {
            // this is a comment line, ignore it
            return;
        }

        if (!this.TryToCreateGoComponentFromModLine(line, out var goComponent))
        {
            var lineTrim = line.Trim();
            this.logger.LogWarning("Line could not be parsed for component [{LineTrim}]", lineTrim);
            singleFileComponentRecorder.RegisterPackageParseFailure(lineTrim);
            return;
        }

        var key = $"{goComponent.Name}@{goComponent.Version}";
        if (replacePathDirectives.Contains(key) || replacePathDirectives.Contains(goComponent.Name))
        {
            this.logger.LogInformation(
                "Skipping {GoComponentId} from {Location} because it's a local reference.",
                goComponent.Id,
                file.Location);
            return;
        }

        if (moduleReplacements.TryGetValue(key, out var replacement) ||
            moduleReplacements.TryGetValue(goComponent.Name, out replacement))
        {
            this.logger.LogInformation(
                "go Module {PackageKey} is being replaced with {TargetName}-{TargetVersion}",
                key,
                replacement.TargetPathOrModule,
                replacement.TargetVersion);
            goComponent = new GoComponent(replacement.TargetPathOrModule, replacement.TargetVersion ?? goComponent.Version);
        }

        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
    }

    private bool TryToCreateGoComponentFromModLine(string line, out GoComponent goComponent)
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

    private async Task<(HashSet<string> ReplacePathDirectives, Dictionary<string, GoReplaceDirective> ModuleReplacements)> GetAllReplaceDirectivesAsync(IComponentStream file)
    {
        var replacePathDirectives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var moduleReplacements = new Dictionary<string, GoReplaceDirective>(StringComparer.OrdinalIgnoreCase);
        const string singleReplaceDirectiveBegin = "replace ";
        const string multiReplaceDirectiveBegin = "replace (";

        using var reader = new StreamReader(file.Stream, leaveOpen: true);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
            {
                continue;
            }

            line = line.Trim();

            // Multiline block: replace (
            if (line.StartsWith(multiReplaceDirectiveBegin))
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    line = line.Trim();
                    if (line == ")")
                    {
                        break;
                    }

                    HandleReplaceDirective(line, replacePathDirectives, moduleReplacements);
                }
            }
            else if (line.StartsWith(singleReplaceDirectiveBegin))
            {
                // single line block: replace
                var directiveContent = line[singleReplaceDirectiveBegin.Length..].Trim();
                HandleReplaceDirective(directiveContent, replacePathDirectives, moduleReplacements);
            }
        }

        return (replacePathDirectives, moduleReplacements);
    }

    private record GoReplaceDirective(string Source, string Version, string TargetPathOrModule, string TargetVersion);
}

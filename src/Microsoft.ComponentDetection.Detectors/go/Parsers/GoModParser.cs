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
    /// <param name="replaceDirectives">HashSet where the token is placed if replace directive substitutes a  local path.</param>
    private static void TryExtractReplaceDirective(string directiveLine, HashSet<string> replaceDirectives)
    {
        var parts = directiveLine.Split("=>", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var source = parts[0].Trim().Split(' ')[0];
            var target = parts[1].Trim();

            if (IsLocalPath(target))
            {
                replaceDirectives.Add(source);
            }
        }
    }

    public async Task<bool> ParseAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord record)
    {
        // Collect replace directives that point to a local path
        var replaceDirectives = await this.GetAllReplacePathDirectivesAsync(file);

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
                    this.TryRegisterDependencyFromModLine(file, line[StartString.Length..], singleFileComponentRecorder, replaceDirectives);
                }

                line = await reader.ReadLineAsync();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = await reader.ReadLineAsync()) != null && !line.EndsWith(')'))
            {
                this.TryRegisterDependencyFromModLine(file, line, singleFileComponentRecorder, replaceDirectives);
            }
        }

        return true;
    }

    private void TryRegisterDependencyFromModLine(IComponentStream file, string line, ISingleFileComponentRecorder singleFileComponentRecorder, HashSet<string> replaceDirectives)
    {
        if (line.Trim().StartsWith("//"))
        {
            // this is a comment line, ignore it
            return;
        }

        if (this.TryToCreateGoComponentFromModLine(line, out var goComponent))
        {
            if (replaceDirectives.Contains(goComponent.Name))
            {
                // Skip registering this dependency since it's replaced by a local path
                // we will be reading this dependency somewhere else
                this.logger.LogInformation("Skipping {GoComponentId} from {Location} because it's a local reference.", goComponent.Id, file.Location);
                return;
            }

            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
        }
        else
        {
            var lineTrim = line.Trim();
            this.logger.LogWarning("Line could not be parsed for component [{LineTrim}]", lineTrim);
            singleFileComponentRecorder.RegisterPackageParseFailure(lineTrim);
        }
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

    private async Task<HashSet<string>> GetAllReplacePathDirectivesAsync(IComponentStream file)
    {
        var replacedDirectives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string singleReplaceDirectiveBegin = "replace ";
        const string multiReplaceDirectiveBegin = "replace (";
        using (var reader = new StreamReader(file.Stream, leaveOpen: true))
        {
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

                        TryExtractReplaceDirective(line, replacedDirectives);
                    }
                }
                else if (line.StartsWith(singleReplaceDirectiveBegin))
                {
                    // single line block: replace
                    var directiveContent = line[singleReplaceDirectiveBegin.Length..].Trim();
                    TryExtractReplaceDirective(directiveContent, replacedDirectives);
                }
            }
        }

        return replacedDirectives;
    }
}

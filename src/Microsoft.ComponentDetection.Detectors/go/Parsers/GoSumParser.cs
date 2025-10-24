#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class GoSumParser : IGoParser
{
    private static readonly Regex GoSumRegex = new(
        @"(?<name>.*)\s+(?<version>.*?)(/go\.mod)?\s+(?<hash>.*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

    private readonly ILogger logger;

    public GoSumParser(ILogger logger) => this.logger = logger;

    // For more information about the format of the go.sum file
    // visit https://golang.org/cmd/go/#hdr-Module_authentication_using_go_sum
    public async Task<bool> ParseAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord record)
    {
        using var reader = new StreamReader(file.Stream);

        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (this.TryToCreateGoComponentFromSumLine(line, out var goComponent))
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
            }
            else
            {
                var lineTrim = line.Trim();
                this.logger.LogWarning("Line could not be parsed for component [{LineTrim}]", lineTrim);
                singleFileComponentRecorder.RegisterPackageParseFailure(lineTrim);
            }
        }

        return true;
    }

    private bool TryToCreateGoComponentFromSumLine(string line, out GoComponent goComponent)
    {
        var m = GoSumRegex.Match(line);
        if (m.Success)
        {
            goComponent = new GoComponent(m.Groups["name"].Value, m.Groups["version"].Value, m.Groups["hash"].Value);
            return true;
        }

        goComponent = null;
        return false;
    }
}

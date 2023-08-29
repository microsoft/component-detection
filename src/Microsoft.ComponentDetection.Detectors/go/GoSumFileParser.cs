namespace Microsoft.ComponentDetection.Detectors.Go;

using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Utility class for parsing the go.sum file.
/// </summary>
internal sealed class GoSumFileParser
{
    private static readonly Regex GoSumRegex = new(
        @"(?<name>.*)\s+(?<version>.*?)(/go\.mod)?\s+(?<hash>.*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses the go.sum file and registers the components found in it.
    /// </summary>
    /// <remarks>
    /// For more information about the format of the go.sum file
    /// visit https://golang.org/cmd/go/#hdr-Module_authentication_using_go_sum.
    /// </remarks>
    /// <param name="singleFileComponentRecorder">The component recorder.</param>
    /// <param name="file">The component stream of the go.sum file.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task ParseGoSumFileAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        ILogger logger)
    {
        using var reader = new StreamReader(file.Stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (TryToCreateGoComponentFromSumLine(line, out var goComponent))
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
    }

    private static bool TryToCreateGoComponentFromSumLine(string line, out GoComponent goComponent)
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

namespace Microsoft.ComponentDetection.Detectors.Go;

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

    public async Task<bool> ParseAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord record)
    {
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
                    this.TryRegisterDependencyFromModLine(line[StartString.Length..], singleFileComponentRecorder);
                }

                line = await reader.ReadLineAsync();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = await reader.ReadLineAsync()) != null && !line.EndsWith(')'))
            {
                this.TryRegisterDependencyFromModLine(line, singleFileComponentRecorder);
            }
        }

        return true;
    }

    private void TryRegisterDependencyFromModLine(string line, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        if (line.Trim().StartsWith("//"))
        {
            // this is a comment line, ignore it
            return;
        }

        if (this.TryToCreateGoComponentFromModLine(line, out var goComponent))
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
}

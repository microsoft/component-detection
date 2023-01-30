namespace Microsoft.ComponentDetection.Detectors.Gradle;
using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[Export(typeof(IComponentDetector))]
public class GradleComponentDetector : FileComponentDetector, IComponentDetector
{
    private static readonly Regex StartsWithLetterRegex = new Regex("^[A-Za-z]", RegexOptions.Compiled);

    public override string Id { get; } = "Gradle";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Maven) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "*.lockfile" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Maven };

    public override int Version { get; } = 2;

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogVerbose("Found Gradle lockfile: " + file.Location);
        ParseLockfile(singleFileComponentRecorder, file);

        return Task.CompletedTask;
    }

    private static void ParseLockfile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream file)
    {
        string text;
        using (var reader = new StreamReader(file.Stream))
        {
            text = reader.ReadToEnd();
        }

        var lines = new List<string>(text.Split("\n"));

        while (lines.Count > 0)
        {
            var line = lines[0].Trim();
            lines.RemoveAt(0);

            if (!StartsWithLetter(line))
            {
                continue;
            }

            if (line.Split(":").Length == 3)
            {
                var detectedMavenComponent = new DetectedComponent(CreateMavenComponentFromFileLine(line));
                singleFileComponentRecorder.RegisterUsage(detectedMavenComponent);
            }
        }
    }

    private static MavenComponent CreateMavenComponentFromFileLine(string line)
    {
        var equalsSeparatorIndex = line.IndexOf('=');
        var isSingleLockfilePerProjectFormat = equalsSeparatorIndex != -1;
        var componentDescriptor = isSingleLockfilePerProjectFormat ? line[..equalsSeparatorIndex] : line;
        var splits = componentDescriptor.Trim().Split(":");
        var groupId = splits[0];
        var artifactId = splits[1];
        var version = splits[2];

        return new MavenComponent(groupId, artifactId, version);
    }

    private static bool StartsWithLetter(string input) => StartsWithLetterRegex.IsMatch(input);
}

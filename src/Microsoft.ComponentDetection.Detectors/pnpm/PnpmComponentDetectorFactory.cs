#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory responsible for constructing the proper <see cref="IPnpmDetector"/> and recording its dependency
/// graph based on the file found during file component detection.
/// </summary>
public class PnpmComponentDetectorFactory : FileComponentDetector
{
    /// <summary>
    /// The maximum version of the report specification that this detector can handle.
    /// </summary>
    private static readonly Version MaxLockfileVersion = new(9, 0);

    public PnpmComponentDetectorFactory(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<PnpmComponentDetectorFactory> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "Pnpm";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Npm)];

    public override IList<string> SearchPatterns { get; } = ["shrinkwrap.yaml", "pnpm-lock.yaml"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Npm];

    public override int Version { get; } = 8;

    public override bool NeedsAutomaticRootDependencyCalculation => true;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;
        this.Logger.LogDebug("Found yaml file: {YamlFile}", file.Location);
        var skippedFolder = this.SkippedFolders.FirstOrDefault(folder => file.Location.Contains(folder));
        if (!string.IsNullOrEmpty(skippedFolder))
        {
            this.Logger.LogDebug("Skipping found file, it was detected as being within a {SkippedFolder} folder.", skippedFolder);
        }

        try
        {
            var fileContent = await new StreamReader(file.Stream).ReadToEndAsync(cancellationToken);
            var detector = this.GetPnpmComponentDetector(fileContent, out var detectedVersion);
            if (detector == null)
            {
                this.Logger.LogWarning("Unsupported lockfileVersion in pnpm yaml file {File}", file.Location);
                using var unsupportedVersionRecord = new InvalidParseVersionTelemetryRecord
                {
                    DetectorId = this.Id,
                    FilePath = file.Location,
                    Version = detectedVersion,
                    MaxVersion = MaxLockfileVersion.ToString(),
                };
            }
            else
            {
                this.Logger.LogDebug(
                        "Found Pnmp yaml file '{Location}' with version '{Version}' so using PnpmDetector of type '{Type}'.",
                        file.Location,
                        detectedVersion ?? "null",
                        detector.GetType().Name);

                detector.RecordDependencyGraphFromFile(fileContent, singleFileComponentRecorder);
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read pnpm yaml file {File}", file.Location);

            using var failedParsingRecord = new FailedParsingFileRecord
            {
                DetectorId = this.Id,
                FilePath = file.Location,
                ExceptionMessage = e.Message,
                StackTrace = e.StackTrace,
            };
        }
    }

    private IPnpmDetector GetPnpmComponentDetector(string fileContent, out string detectedVersion)
    {
        detectedVersion = PnpmParsingUtilitiesFactory.DeserializePnpmYamlFileVersion(fileContent);
        this.RecordLockfileVersion(detectedVersion);
        var majorVersion = detectedVersion?.Split(".")[0];
        return majorVersion switch
        {
            // The null case falls through to version 5 to preserve the behavior of this scanner from before version specific logic was added.
            // This allows files versioned with "shrinkwrapVersion" (such as one included in some of the tests) to be used.
            // Given that "shrinkwrapVersion" is a concept from file format version 4 https://github.com/pnpm/spec/blob/master/lockfile/4.md)
            // this case might not be robust.
            null => new Pnpm5Detector(),
            Pnpm5Detector.MajorVersion => new Pnpm5Detector(),
            Pnpm6Detector.MajorVersion => new Pnpm6Detector(),
            Pnpm9Detector.MajorVersion => new Pnpm9Detector(),
            _ => null,
        };
    }
}

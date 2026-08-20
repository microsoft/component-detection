#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Gradle;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class GradleComponentDetector : FileComponentDetector, IComponentDetector
{
    private const string DevConfigurationsEnvVar = "CD_GRADLE_DEV_CONFIGURATIONS";
    private const string DevLockfilesEnvVar = "CD_GRADLE_DEV_LOCKFILES";
    private static readonly Regex StartsWithLetterRegex = new Regex("^[A-Za-z]", RegexOptions.Compiled);

    private readonly List<string> devConfigurations;
    private readonly List<string> devLockfiles;

    public GradleComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IEnvironmentVariableService envVarService,
        ILogger<GradleComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;

        this.devLockfiles = envVarService.GetListEnvironmentVariable(DevLockfilesEnvVar) ?? [];
        this.devConfigurations = envVarService.GetListEnvironmentVariable(DevConfigurationsEnvVar) ?? [];
        this.Logger.LogDebug("Gradle dev-only lockfiles {Lockfiles}", string.Join(", ", this.devLockfiles));
        this.Logger.LogDebug("Gradle dev-only configurations {Configurations}", string.Join(", ", this.devConfigurations));
    }

    public override string Id { get; } = "Gradle";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Maven)];

    public override IList<string> SearchPatterns { get; } = ["*.lockfile"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Maven];

    public override int Version { get; } = 3;

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogDebug("Found Gradle lockfile: {Location}", file.Location);
        this.ParseLockfile(singleFileComponentRecorder, file);

        return Task.CompletedTask;
    }

    private void ParseLockfile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream file)
    {
        string text;
        using (var reader = new StreamReader(file.Stream))
        {
            text = reader.ReadToEnd();
        }

        var lines = new List<string>(text.Split("\n"));
        var devDepLockFile = this.IsDevDependencyByLockfile(file);

        while (lines.Count > 0)
        {
            var line = lines[0].Trim();
            lines.RemoveAt(0);

            if (!this.StartsWithLetter(line))
            {
                continue;
            }

            if (line.Split(":").Length == 3)
            {
                var detectedMavenComponent = new DetectedComponent(this.CreateMavenComponentFromFileLine(line));
                var devDependency = devDepLockFile || this.IsDevDependencyByConfigurations(line);
                singleFileComponentRecorder.RegisterUsage(detectedMavenComponent, isDevelopmentDependency: devDependency);
            }
        }
    }

    private MavenComponent CreateMavenComponentFromFileLine(string line)
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

    private bool StartsWithLetter(string input) => StartsWithLetterRegex.IsMatch(input);

    private bool IsDevDependencyByConfigurations(string line)
    {
        var equalsSeparatorIndex = line.IndexOf('=');
        if (equalsSeparatorIndex == -1)
        {
            // We can't parse out the configuration. Maybe the project is using the one-lockfile-per-configuration format but
            // this is deprecated in Gradle so we don't support it here, projects should upgrade to one-lockfile-per-project.
            return false;
        }

        var configurations = line[(equalsSeparatorIndex + 1)..].Split(",");
        return configurations.All(this.IsDevDependencyByConfigurationName);
    }

    private bool IsDevDependencyByConfigurationName(string configurationName)
    {
        return this.devConfigurations.Contains(configurationName);
    }

    private bool IsDevDependencyByLockfile(IComponentStream file)
    {
        // Buildscript and Settings lockfiles are always development dependencies
        var lockfileName = Path.GetFileName(file.Location);
        var lockfileRelativePath = Path.GetRelativePath(this.CurrentScanRequest.SourceDirectory.FullName, file.Location);
        var dev = lockfileName == "buildscript-gradle.lockfile"
            || lockfileName == "settings-gradle.lockfile"
            || this.devLockfiles.Contains(lockfileRelativePath);

        if (dev)
        {
            this.Logger.LogDebug("Gradle lockfile {Location} contains dev dependencies only", lockfileRelativePath);
        }
        else
        {
            this.Logger.LogDebug("Gradle lockfile {Location} contains at least some production dependencies", lockfileRelativePath);
        }

        return dev;
    }
}

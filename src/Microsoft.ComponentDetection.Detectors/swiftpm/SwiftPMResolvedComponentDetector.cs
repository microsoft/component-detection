namespace Microsoft.ComponentDetection.Detectors.SwiftPM;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

/// <summary>
/// Detects SwiftPM components.
/// </summary>
public class SwiftPMResolvedComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public SwiftPMResolvedComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<SwiftPMResolvedComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "SwiftPM";

    public override IEnumerable<string> Categories => [Enum.GetName(DetectorClass.SwiftPM)];

    public override IList<string> SearchPatterns { get; } = ["Package.resolved"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.SwiftPM];

    public override int Version => 2;

    protected override Task OnFileFoundAsync(
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this.ProcessPackageResolvedFile(processRequest.SingleFileComponentRecorder, processRequest.ComponentStream);
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "SwiftPMComponentDetector: Error processing Package.resolved file: {Location}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    private void ProcessPackageResolvedFile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream componentStream)
    {
        var parsedResolvedFile = this.ReadAndParseResolvedFile(componentStream.Stream);

        foreach (var package in parsedResolvedFile.Pins)
        {
            // We are only interested in packages coming from remote sources such as git
            // The Package Kind is not an enum because the SwiftPM contract does not specify the possible values.
            var targetSwiftPackageKind = "remoteSourceControl";
            if (package.Kind == targetSwiftPackageKind)
            {
                // The version of the package is not always available.
                var version = package.State.Version ?? package.State.Branch ?? package.State.Revision;

                var detectedSwiftPMComponent = new SwiftPMComponent(
                    name: package.Identity,
                    version: version,
                    packageUrl: package.Location,
                    hash: package.State.Revision);
                var newDetectedSwiftComponent = new DetectedComponent(component: detectedSwiftPMComponent, detector: this);
                singleFileComponentRecorder.RegisterUsage(newDetectedSwiftComponent);

                // We also register a Git component for the same package so that the git URL is registered.
                // SwiftPM directly downloads the package from the git URL.
                var detectedGitComponent = new GitComponent(
                    repositoryUrl: new Uri(package.Location),
                    commitHash: package.State.Revision,
                    tag: version);
                var newDetectedGitComponent = new DetectedComponent(component: detectedGitComponent, detector: this);
                singleFileComponentRecorder.RegisterUsage(newDetectedGitComponent);
            }
        }
    }

    /// <summary>
    /// Reads the stream of the package resolved file and parses it.
    /// </summary>
    /// <param name="stream">The stream of the file to parse.</param>
    /// <returns>The parsed object.</returns>
    private SwiftPMResolvedFile ReadAndParseResolvedFile(Stream stream)
    {
        string resolvedFile;
        using (var reader = new StreamReader(stream))
        {
            resolvedFile = reader.ReadToEnd();
        }

        return JsonConvert.DeserializeObject<SwiftPMResolvedFile>(resolvedFile);
    }
}

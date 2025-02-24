namespace Microsoft.ComponentDetection.Detectors.Swift;

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
/// Detects Swift Package Manager components.
/// </summary>
public class SwiftResolvedComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    // We are only interested in packages coming from remote sources such as git
    // The Package Kind is not an enum because the Swift Package Manager contract does not specify the possible values.
    private const string TargetSwiftPackageKind = "remoteSourceControl";

    public SwiftResolvedComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<SwiftResolvedComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "Swift";

    public override IEnumerable<string> Categories => [Enum.GetName(DetectorClass.Swift)];

    public override IList<string> SearchPatterns { get; } = ["Package.resolved"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Swift];

    public override int Version => 1;

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
            this.Logger.LogError(exception, "SwiftComponentDetector: Error processing Package.resolved file: {Location}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    private void ProcessPackageResolvedFile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream componentStream)
    {
        var parsedResolvedFile = this.ReadAndParseResolvedFile(componentStream.Stream);

        foreach (var package in parsedResolvedFile.Pins)
        {
            try
            {
                if (package.Kind == TargetSwiftPackageKind)
                {
                    // The version of the package is not always available.
                    var version = package.State.Version ?? package.State.Branch ?? package.State.Revision;

                    var detectedSwiftComponent = new SwiftComponent(
                        name: package.Identity,
                        version: version,
                        packageUrl: package.Location,
                        hash: package.State.Revision);
                    var newDetectedSwiftComponent = new DetectedComponent(component: detectedSwiftComponent);
                    singleFileComponentRecorder.RegisterUsage(newDetectedSwiftComponent);

                    // We also register a Git component for the same package so that the git URL is registered.
                    // Swift Package Manager directly downloads the package from the git URL.
                    var detectedGitComponent = new GitComponent(
                        repositoryUrl: new Uri(package.Location),
                        commitHash: package.State.Revision,
                        tag: version);
                    var newDetectedGitComponent = new DetectedComponent(component: detectedGitComponent);
                    singleFileComponentRecorder.RegisterUsage(newDetectedGitComponent);
                }
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "SwiftComponentDetector: Error processing package: {Package}", package.Identity);
            }
        }
    }

    /// <summary>
    /// Reads the stream of the package resolved file and parses it.
    /// </summary>
    /// <param name="stream">The stream of the file to parse.</param>
    /// <returns>The parsed object.</returns>
    private SwiftResolvedFile ReadAndParseResolvedFile(Stream stream)
    {
        string resolvedFile;
        using (var reader = new StreamReader(stream))
        {
            resolvedFile = reader.ReadToEnd();
        }

        return JsonConvert.DeserializeObject<SwiftResolvedFile>(resolvedFile);
    }
}

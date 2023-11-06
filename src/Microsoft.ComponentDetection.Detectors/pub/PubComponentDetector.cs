namespace Microsoft.ComponentDetection.Detectors.Pub;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

public class PubComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public PubComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<PubComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "pub";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Pub) };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Pub };

    public override int Version => 1;

    public override IList<string> SearchPatterns => new List<string> { "pubspec.lock" };

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        try
        {
            using var reader = new StreamReader(processRequest.ComponentStream.Stream);
            var text = await reader.ReadToEndAsync();

            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var parsedFile = deserializer.Deserialize<PubSpecLock>(text);
            this.Logger.LogDebug("SDK {Dart}", parsedFile.Sdks.Dart);

            foreach (var package in parsedFile.Packages)
            {
                if (package.Value.Source == "hosted")
                {
                    var component = new PubComponent(
                        package.Value.GetName(),
                        package.Value.Version,
                        package.Value.Dependency,
                        package.Value.GetSha256(),
                        package.Value.GePackageDownloadedSource());
                    this.Logger.LogInformation("Registering component {Package}", component);

                    processRequest.SingleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error while parsing lock file");
        }
    }
}

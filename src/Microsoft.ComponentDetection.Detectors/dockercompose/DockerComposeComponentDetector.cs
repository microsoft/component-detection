#nullable disable
namespace Microsoft.ComponentDetection.Detectors.DockerCompose;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using YamlDotNet.RepresentationModel;

public class DockerComposeComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public DockerComposeComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<DockerComposeComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "DockerCompose";

    public override IList<string> SearchPatterns { get; } =
    [
        "docker-compose.yml", "docker-compose.yaml",
        "docker-compose.*.yml", "docker-compose.*.yaml",
        "compose.yml", "compose.yaml",
        "compose.*.yml", "compose.*.yaml",
    ];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.DockerReference];

    public override int Version => 1;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.DockerCompose)];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        try
        {
            this.Logger.LogInformation("Discovered Docker Compose file: {Location}", file.Location);

            string contents;
            using (var reader = new StreamReader(file.Stream))
            {
                contents = await reader.ReadToEndAsync(cancellationToken);
            }

            var yaml = new YamlStream();
            yaml.Load(new StringReader(contents));

            if (yaml.Documents.Count == 0)
            {
                return;
            }

            foreach (var document in yaml.Documents)
            {
                if (document.RootNode is YamlMappingNode rootMapping)
                {
                    this.ExtractImageReferences(rootMapping, singleFileComponentRecorder, file.Location);
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to parse Docker Compose file: {Location}", file.Location);
        }
    }

    private static YamlMappingNode GetMappingChild(YamlMappingNode parent, string key)
    {
        foreach (var entry in parent.Children)
        {
            if (entry.Key is YamlScalarNode scalarKey && string.Equals(scalarKey.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value as YamlMappingNode;
            }
        }

        return null;
    }

    private void ExtractImageReferences(YamlMappingNode rootMapping, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        var services = GetMappingChild(rootMapping, "services");
        if (services == null)
        {
            return;
        }

        foreach (var serviceEntry in services.Children)
        {
            if (serviceEntry.Value is not YamlMappingNode serviceMapping)
            {
                continue;
            }

            // Extract direct image: references
            foreach (var entry in serviceMapping.Children)
            {
                var key = (entry.Key as YamlScalarNode)?.Value;
                if (string.Equals(key, "image", StringComparison.OrdinalIgnoreCase))
                {
                    var imageRef = (entry.Value as YamlScalarNode)?.Value;
                    if (!string.IsNullOrWhiteSpace(imageRef))
                    {
                        this.TryRegisterImageReference(imageRef, recorder, fileLocation);
                    }
                }
            }
        }
    }

    private void TryRegisterImageReference(string imageReference, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        try
        {
            var dockerRef = DockerReferenceUtility.ParseFamiliarName(imageReference);
            if (dockerRef != null)
            {
                recorder.RegisterUsage(new DetectedComponent(dockerRef.ToTypedDockerReferenceComponent()));
            }
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Failed to parse image reference '{ImageReference}' in {Location}", imageReference, fileLocation);
        }
    }
}

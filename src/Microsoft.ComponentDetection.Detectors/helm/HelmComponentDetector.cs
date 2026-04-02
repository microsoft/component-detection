#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Helm;

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

public class HelmComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public HelmComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<HelmComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "Helm";

    public override IList<string> SearchPatterns { get; } = ["Chart.yaml", "values.yaml"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.DockerReference];

    public override int Version => 1;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Helm)];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        try
        {
            this.Logger.LogInformation("Discovered Helm file: {Location}", file.Location);

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

            var fileName = Path.GetFileName(file.Location);

            if (string.Equals(fileName, "values.yaml", StringComparison.OrdinalIgnoreCase))
            {
                this.ExtractImageReferencesFromValues(yaml, singleFileComponentRecorder, file.Location);
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to parse Helm file: {Location}", file.Location);
        }
    }

    private void ExtractImageReferencesFromValues(YamlStream yaml, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        foreach (var document in yaml.Documents)
        {
            if (document.RootNode is YamlMappingNode rootMapping)
            {
                this.WalkYamlForImages(rootMapping, recorder, fileLocation);
            }
        }
    }

    /// <summary>
    /// Walks the YAML tree looking for image references. Handles two common patterns:
    /// 1. Direct image string: `image: nginx:1.21`
    /// 2. Structured image object: `image: { repository: nginx, tag: "1.21" }`.
    /// </summary>
    private void WalkYamlForImages(YamlMappingNode mapping, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        foreach (var entry in mapping.Children)
        {
            var key = (entry.Key as YamlScalarNode)?.Value;

            if (string.Equals(key, "image", StringComparison.OrdinalIgnoreCase))
            {
                switch (entry.Value)
                {
                    // image: nginx:1.21
                    case YamlScalarNode scalarValue when !string.IsNullOrWhiteSpace(scalarValue.Value):
                        this.TryRegisterImageReference(scalarValue.Value, recorder, fileLocation);
                        break;

                    // image:
                    //   repository: nginx
                    //   tag: "1.21"
                    case YamlMappingNode imageMapping:
                        this.TryRegisterStructuredImageReference(imageMapping, recorder, fileLocation);
                        break;
                }
            }
            else if (entry.Value is YamlMappingNode childMapping)
            {
                this.WalkYamlForImages(childMapping, recorder, fileLocation);
            }
            else if (entry.Value is YamlSequenceNode sequenceNode)
            {
                foreach (var item in sequenceNode)
                {
                    if (item is YamlMappingNode sequenceMapping)
                    {
                        this.WalkYamlForImages(sequenceMapping, recorder, fileLocation);
                    }
                }
            }
        }
    }

    private void TryRegisterStructuredImageReference(YamlMappingNode imageMapping, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        string repository = null;
        string tag = null;
        string digest = null;
        string registry = null;

        foreach (var child in imageMapping.Children)
        {
            var childKey = (child.Key as YamlScalarNode)?.Value;
            var childValue = (child.Value as YamlScalarNode)?.Value;

            switch (childKey?.ToLowerInvariant())
            {
                case "repository":
                    repository = childValue;
                    break;
                case "tag":
                    tag = childValue;
                    break;
                case "digest":
                    digest = childValue;
                    break;
                case "registry":
                    registry = childValue;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(repository))
        {
            return;
        }

        var imageRef = !string.IsNullOrWhiteSpace(registry)
            ? $"{registry}/{repository}"
            : repository;

        if (!string.IsNullOrWhiteSpace(digest))
        {
            imageRef = $"{imageRef}@{digest}";
        }
        else if (!string.IsNullOrWhiteSpace(tag))
        {
            imageRef = $"{imageRef}:{tag}";
        }

        this.TryRegisterImageReference(imageRef, recorder, fileLocation);
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

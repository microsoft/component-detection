#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Kubernetes;

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
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

public class KubernetesComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private static readonly HashSet<string> KubernetesKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pod",
        "PodTemplate",
        "Deployment",
        "StatefulSet",
        "DaemonSet",
        "ReplicaSet",
        "Job",
        "CronJob",
        "ReplicationController",
    };

    public KubernetesComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<KubernetesComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "Kubernetes";

    public override IList<string> SearchPatterns { get; } = ["*.yaml", "*.yml"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.DockerReference];

    public override int Version => 1;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Kubernetes)!];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        try
        {
            string contents;
            using (var reader = new StreamReader(file.Stream))
            {
                contents = await reader.ReadToEndAsync(cancellationToken);
            }

            // Skip files that aren't Kubernetes manifests.
            if (!contents.Contains("apiVersion", StringComparison.Ordinal) ||
                !contents.Contains("kind", StringComparison.Ordinal))
            {
                return;
            }

            var yaml = new YamlStream();
            yaml.Load(new StringReader(contents));

            foreach (var document in yaml.Documents)
            {
                if (document.RootNode is not YamlMappingNode rootMapping)
                {
                    continue;
                }

                if (!this.IsKubernetesManifest(rootMapping))
                {
                    continue;
                }

                this.Logger.LogInformation("Discovered Kubernetes manifest: {Location}", file.Location);
                this.ExtractImageReferences(rootMapping, singleFileComponentRecorder, file.Location);
            }
        }
        catch (YamlException e)
        {
            this.Logger.LogWarning(e, "Failed to parse YAML file: {Location}", file.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Unexpected error processing file: {Location}", file.Location);
        }
    }

    private static YamlMappingNode? GetMappingChild(YamlMappingNode parent, string key)
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

    private static YamlSequenceNode? GetSequenceChild(YamlMappingNode parent, string key)
    {
        foreach (var entry in parent.Children)
        {
            if (entry.Key is YamlScalarNode scalarKey && string.Equals(scalarKey.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value as YamlSequenceNode;
            }
        }

        return null;
    }

    private bool IsKubernetesManifest(YamlMappingNode rootMapping)
    {
        string? apiVersion = null;
        string? kind = null;

        foreach (var entry in rootMapping.Children)
        {
            var entryKey = (entry.Key as YamlScalarNode)?.Value;
            if (string.Equals(entryKey, "apiVersion", StringComparison.OrdinalIgnoreCase))
            {
                apiVersion = (entry.Value as YamlScalarNode)?.Value;
            }
            else if (string.Equals(entryKey, "kind", StringComparison.OrdinalIgnoreCase))
            {
                kind = (entry.Value as YamlScalarNode)?.Value;
            }
        }

        return !string.IsNullOrEmpty(apiVersion) && !string.IsNullOrEmpty(kind) && KubernetesKinds.Contains(kind);
    }

    private void ExtractImageReferences(YamlMappingNode rootMapping, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        // For Pod, the spec is at the top level
        // For Deployment/StatefulSet/etc, the pod spec is at spec.template.spec
        var spec = GetMappingChild(rootMapping, "spec");
        if (spec == null)
        {
            return;
        }

        // Direct pod spec (kind: Pod)
        this.ExtractContainerImages(spec, recorder, fileLocation);

        // Templated pod spec (kind: Deployment, StatefulSet, etc.)
        var template = GetMappingChild(spec, "template");
        if (template != null)
        {
            var templateSpec = GetMappingChild(template, "spec");
            if (templateSpec != null)
            {
                this.ExtractContainerImages(templateSpec, recorder, fileLocation);
            }
        }

        // CronJob has spec.jobTemplate.spec.template.spec
        var jobTemplate = GetMappingChild(spec, "jobTemplate");
        if (jobTemplate != null)
        {
            var jobSpec = GetMappingChild(jobTemplate, "spec");
            if (jobSpec != null)
            {
                var jobPodTemplate = GetMappingChild(jobSpec, "template");
                if (jobPodTemplate != null)
                {
                    var jobPodSpec = GetMappingChild(jobPodTemplate, "spec");
                    if (jobPodSpec != null)
                    {
                        this.ExtractContainerImages(jobPodSpec, recorder, fileLocation);
                    }
                }
            }
        }
    }

    private void ExtractContainerImages(YamlMappingNode podSpec, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        this.ExtractImagesFromContainerList(podSpec, "containers", recorder, fileLocation);
        this.ExtractImagesFromContainerList(podSpec, "initContainers", recorder, fileLocation);
        this.ExtractImagesFromContainerList(podSpec, "ephemeralContainers", recorder, fileLocation);
    }

    private void ExtractImagesFromContainerList(YamlMappingNode podSpec, string containerKey, ISingleFileComponentRecorder recorder, string fileLocation)
    {
        var containers = GetSequenceChild(podSpec, containerKey);
        if (containers == null)
        {
            return;
        }

        foreach (var container in containers)
        {
            if (container is not YamlMappingNode containerMapping)
            {
                continue;
            }

            foreach (var entry in containerMapping.Children)
            {
                var key = (entry.Key as YamlScalarNode)?.Value;
                if (string.Equals(key, "image", StringComparison.OrdinalIgnoreCase))
                {
                    var imageRef = (entry.Value as YamlScalarNode)?.Value;
                    if (!string.IsNullOrWhiteSpace(imageRef))
                    {
                        DockerReferenceUtility.TryRegisterImageReference(imageRef, recorder);
                    }
                }
            }
        }
    }
}

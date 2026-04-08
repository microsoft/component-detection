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

    public override IEnumerable<string> Categories => [nameof(DetectorClass.Kubernetes)];

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

            // Fast text-based rejection before expensive YAML parsing.
            if (!LooksLikeKubernetesManifest(contents))
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

                if (!IsKubernetesManifest(rootMapping))
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

    /// <summary>
    /// Fast text-based pre-filter. Checks for "apiVersion" and a known "kind: &lt;K8sKind&gt;"
    /// pattern using line-based scanning to reject non-Kubernetes YAML without YAML parsing.
    /// Tolerates varied whitespace (e.g. "kind:Deployment", "kind:   Deployment") and
    /// optional quotes around the value.
    /// </summary>
    private static bool LooksLikeKubernetesManifest(string contents)
    {
        var span = contents.AsSpan();

        // Must contain apiVersion to be any kind of K8s manifest.
        if (span.IndexOf("apiVersion".AsSpan(), StringComparison.Ordinal) < 0)
        {
            return false;
        }

        // Scan line-by-line for a "kind: <K8sResource>" entry, tolerating varied
        // whitespace and quotes to avoid false negatives on valid manifests.
        foreach (var line in span.EnumerateLines())
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("kind", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var afterKind = trimmed.Slice(4).TrimStart();
            if (afterKind.IsEmpty || afterKind[0] != ':')
            {
                continue;
            }

            var value = afterKind.Slice(1).Trim();

            // Strip inline YAML comments (K8s kind values never contain '#').
            var commentIdx = value.IndexOf('#');
            if (commentIdx >= 0)
            {
                value = value.Slice(0, commentIdx).TrimEnd();
            }

            // Strip optional surrounding quotes (e.g. kind: "Deployment").
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value.Slice(1, value.Length - 2).Trim();
            }

            if (!value.IsEmpty && KubernetesKinds.Contains(value.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKubernetesManifest(YamlMappingNode rootMapping)
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

            // Both fields found — stop iterating remaining keys.
            if (apiVersion != null && kind != null)
            {
                break;
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

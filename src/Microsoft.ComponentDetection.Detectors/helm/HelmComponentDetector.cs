namespace Microsoft.ComponentDetection.Detectors.Helm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using YamlDotNet.RepresentationModel;

public class HelmComponentDetector : FileComponentDetector
{
    /// <summary>
    /// Maximum size (in bytes) of a values file the detector will parse. The "*values*" globs
    /// can match large, non-Helm YAML files whose full-DOM parse dominates worst-case runtime;
    /// files above this limit are skipped so a single pathological file cannot exhaust the
    /// detector's time budget.
    /// </summary>
    private const long MaxValuesFileSizeBytes = 20 * 1024 * 1024; // 20 MB

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

    public override IList<string> SearchPatterns { get; } =
    [
        "Chart.yaml", "Chart.yml",
        "*values*.yaml", "*values*.yml",
    ];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.DockerReference];

    public override int Version => 1;

    public override IEnumerable<string> Categories => [nameof(DetectorClass.Helm)];

    /// <summary>
    /// Gets or sets a value indicating whether values files are processed concurrently.
    /// Each file is parsed independently into its own <see cref="ISingleFileComponentRecorder"/>
    /// and <see cref="DockerReferenceUtility"/> is stateless, so parsing is thread-safe and
    /// scales across cores for repositories containing many charts.
    /// </summary>
    protected override bool EnableParallelism { get; set; } = true;

    /// <summary>
    /// Pre-filters scan work to only values files co-located with a Chart.yaml/Chart.yml.
    /// Materializes all matched files, identifies Helm chart directories, then filters.
    /// </summary>
    /// <returns>An observable of only the values-file requests in Helm chart directories.</returns>
    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        var allRequests = await processRequests.ToList().ToTask(cancellationToken);

        var chartDirectories = new HashSet<string>(
            allRequests
                .Where(r => IsChartFile(Path.GetFileName(r.ComponentStream.Location)))
                .Select(r => Path.GetDirectoryName(r.ComponentStream.Location) ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        return allRequests
            .Where(r => IsValuesFile(Path.GetFileName(r.ComponentStream.Location))
                        && chartDirectories.Contains(Path.GetDirectoryName(r.ComponentStream.Location) ?? string.Empty))
            .ToObservable();
    }

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var file = processRequest.ComponentStream;

        // OnPrepareDetectionAsync has already filtered to values files co-located
        // with a Helm chart file (Chart.yaml or Chart.yml), so no further
        // filename/directory checks are needed.
        try
        {
            // Check the size before touching ComponentStream so an oversized file is never
            // buffered into memory. The "*values*" globs can match large, non-Helm YAML files
            // whose full-DOM parse is the main driver of worst-case (timeout) runtime.
            var fileInfo = new FileInfo(file.Location);
            if (fileInfo.Exists && fileInfo.Length > MaxValuesFileSizeBytes)
            {
                this.Logger.LogWarning(
                    "Skipping Helm values file exceeding size limit ({Length} bytes > {Limit} bytes): {Location}",
                    fileInfo.Length,
                    MaxValuesFileSizeBytes,
                    file.Location);
                return Task.CompletedTask;
            }

            this.Logger.LogInformation("Discovered Helm values file: {Location}", file.Location);

            // Parse directly from the stream; the content is already buffered in memory by
            // LazyComponentStream, so reading it into an intermediate string only adds an
            // extra full-file allocation and GC pressure under parallel processing.
            var yaml = new YamlStream();
            using (var reader = new StreamReader(file.Stream))
            {
                yaml.Load(reader);
            }

            if (yaml.Documents.Count == 0)
            {
                return Task.CompletedTask;
            }

            this.ExtractImageReferencesFromValues(yaml, processRequest.SingleFileComponentRecorder);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to parse Helm file: {Location}", file.Location);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if the given file name matches Helm chart file patterns (Chart.yaml or Chart.yml).
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
    /// <returns>True if the file name matches Helm chart file patterns; otherwise, false.</returns>
    /// <remarks> The <c>C</c> in <c>Chart.yaml</c> is case-sensitive <see href="https://helm.sh/docs/chart_best_practices/conventions/#usage-of-the-words-helm-and-chart"/>.</remarks>
    private static bool IsChartFile(string fileName) =>
        fileName.Equals("Chart.yaml", StringComparison.Ordinal) ||
        fileName.Equals("Chart.yml", StringComparison.Ordinal);

    private static bool IsValuesFile(string fileName) =>
        fileName.Contains("values", StringComparison.OrdinalIgnoreCase) &&
        (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
         fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));

    private void ExtractImageReferencesFromValues(YamlStream yaml, ISingleFileComponentRecorder recorder)
    {
        foreach (var document in yaml.Documents)
        {
            if (document.RootNode is YamlMappingNode rootMapping)
            {
                this.WalkYamlForImages(rootMapping, recorder);
            }
        }
    }

    /// <summary>
    /// Walks the YAML tree looking for image references. Handles two common patterns:
    /// 1. Direct image string: `image: nginx:1.21`
    /// 2. Structured image object: `image: { repository: nginx, tag: "1.21" }`.
    /// </summary>
    private void WalkYamlForImages(YamlMappingNode mapping, ISingleFileComponentRecorder recorder)
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
                        DockerReferenceUtility.TryRegisterImageReference(scalarValue.Value, recorder, this.Logger);
                        break;

                    // image:
                    //   repository: nginx
                    //   tag: "1.21"
                    case YamlMappingNode imageMapping:
                        this.TryRegisterStructuredImageReference(imageMapping, recorder);
                        break;

                    default:
                        break;
                }
            }
            else if (entry.Value is YamlMappingNode childMapping)
            {
                this.WalkYamlForImages(childMapping, recorder);
            }
            else if (entry.Value is YamlSequenceNode sequenceNode)
            {
                foreach (var item in sequenceNode)
                {
                    if (item is YamlMappingNode sequenceMapping)
                    {
                        this.WalkYamlForImages(sequenceMapping, recorder);
                    }
                }
            }
        }
    }

    private void TryRegisterStructuredImageReference(YamlMappingNode imageMapping, ISingleFileComponentRecorder recorder)
    {
        string? repository = null;
        string? tag = null;
        string? digest = null;
        string? registry = null;

        foreach (var child in imageMapping.Children)
        {
            var childKey = (child.Key as YamlScalarNode)?.Value;
            var childValue = (child.Value as YamlScalarNode)?.Value;

            if (string.Equals(childKey, "REPOSITORY", StringComparison.OrdinalIgnoreCase))
            {
                repository = childValue;
            }
            else if (string.Equals(childKey, "TAG", StringComparison.OrdinalIgnoreCase))
            {
                tag = childValue;
            }
            else if (string.Equals(childKey, "DIGEST", StringComparison.OrdinalIgnoreCase))
            {
                digest = childValue;
            }
            else if (string.Equals(childKey, "REGISTRY", StringComparison.OrdinalIgnoreCase))
            {
                registry = childValue;
            }
        }

        if (string.IsNullOrWhiteSpace(repository))
        {
            return;
        }

        var imageRef = !string.IsNullOrWhiteSpace(registry)
            ? $"{registry}/{repository}"
            : repository;

        if (!string.IsNullOrWhiteSpace(tag))
        {
            imageRef = $"{imageRef}:{tag}";
        }

        if (!string.IsNullOrWhiteSpace(digest))
        {
            imageRef = $"{imageRef}@{digest}";
        }

        DockerReferenceUtility.TryRegisterImageReference(imageRef, recorder, this.Logger);
    }
}

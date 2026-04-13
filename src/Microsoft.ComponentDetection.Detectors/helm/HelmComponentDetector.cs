#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Helm;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
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
    private readonly ConcurrentDictionary<string, bool> helmChartDirectories = new(StringComparer.OrdinalIgnoreCase);

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

    public override async Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        this.helmChartDirectories.Clear();
        return await base.ExecuteDetectorAsync(request, cancellationToken);
    }

    /// <summary>
    /// Pre-filters scan work to values files that are co-located with a Helm chart.
    ///
    /// This is intentionally implemented as a streaming pipeline (instead of
    /// materializing all matching files with ToList) to reduce peak memory usage and
    /// start emitting work earlier on large repositories.
    ///
    /// Enumeration order is not guaranteed, so values files may be seen before the
    /// corresponding Chart.yaml. To preserve correctness, values files are buffered
    /// per directory until a chart file for that directory is observed, then released.
    /// </summary>
    /// <returns>
    /// A prepared observable that emits only values-file requests belonging to
    /// directories that contain a Chart.yaml/Chart.yml.
    /// </returns>
    protected override Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Observable.Create<ProcessRequest>(observer =>
        {
            // Buffer only values files whose directory has not yet produced a Chart file.
            var pendingValuesByDirectory = new Dictionary<string, List<ProcessRequest>>(StringComparer.OrdinalIgnoreCase);
            var gate = new object();

            var subscription = processRequests.Subscribe(
                request =>
                {
                    var fileName = Path.GetFileName(request.ComponentStream.Location);
                    var directory = Path.GetDirectoryName(request.ComponentStream.Location) ?? string.Empty;

                    // Protect shared state because IObservable callbacks may arrive concurrently.
                    lock (gate)
                    {
                        if (IsChartFile(fileName))
                        {
                            // Mark this directory as a Helm chart directory.
                            this.helmChartDirectories.TryAdd(directory, true);

                            // Release any values files that arrived earlier for this directory.
                            if (pendingValuesByDirectory.Remove(directory, out var pendingRequests))
                            {
                                foreach (var pendingRequest in pendingRequests)
                                {
                                    observer.OnNext(pendingRequest);
                                }
                            }

                            return;
                        }

                        if (!IsValuesFile(fileName))
                        {
                            // Ignore non-values files (Chart files are handled above).
                            return;
                        }

                        if (this.helmChartDirectories.ContainsKey(directory))
                        {
                            // Directory is already known to be a chart; emit immediately.
                            observer.OnNext(request);
                            return;
                        }

                        // Chart file not seen yet for this directory; queue for later release.
                        if (!pendingValuesByDirectory.TryGetValue(directory, out var pendingRequestsForDirectory))
                        {
                            pendingRequestsForDirectory = [];
                            pendingValuesByDirectory[directory] = pendingRequestsForDirectory;
                        }

                        pendingRequestsForDirectory.Add(request);
                    }
                },
                observer.OnError,
                observer.OnCompleted);

            var cancellationRegistration = cancellationToken.Register(() =>
            {
                // Stop forwarding events if detection is cancelled.
                subscription.Dispose();
                observer.OnCompleted();
            });

            return () =>
            {
                cancellationRegistration.Dispose();
                subscription.Dispose();
            };
        }));
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var file = processRequest.ComponentStream;

        // OnPrepareDetectionAsync has already filtered to values files co-located
        // with a Chart.yaml — no further filename/directory checks are needed.
        try
        {
            this.Logger.LogInformation("Discovered Helm values file: {Location}", file.Location);

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

            this.ExtractImageReferencesFromValues(yaml, processRequest.SingleFileComponentRecorder, file.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to parse Helm file: {Location}", file.Location);
        }
    }

    private static bool IsChartFile(string fileName) =>
        fileName.Equals("Chart.yaml", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("Chart.yml", StringComparison.OrdinalIgnoreCase);

    private static bool IsValuesFile(string fileName) =>
        fileName.Contains("values", StringComparison.OrdinalIgnoreCase) &&
        (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
         fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));

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
                        DockerReferenceUtility.TryRegisterImageReference(scalarValue.Value, recorder);
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

            switch (childKey?.ToUpperInvariant())
            {
                case "REPOSITORY":
                    repository = childValue;
                    break;
                case "TAG":
                    tag = childValue;
                    break;
                case "DIGEST":
                    digest = childValue;
                    break;
                case "REGISTRY":
                    registry = childValue;
                    break;
                default:
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

        if (!string.IsNullOrWhiteSpace(tag))
        {
            imageRef = $"{imageRef}:{tag}";
        }

        if (!string.IsNullOrWhiteSpace(digest))
        {
            imageRef = $"{imageRef}@{digest}";
        }

        DockerReferenceUtility.TryRegisterImageReference(imageRef, recorder);
    }
}

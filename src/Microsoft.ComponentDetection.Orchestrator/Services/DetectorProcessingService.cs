namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Globbing;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static System.Environment;

public class DetectorProcessingService : IDetectorProcessingService
{
    private readonly IObservableDirectoryWalkerFactory scanner;
    private readonly ILogger<DetectorProcessingService> logger;

    public DetectorProcessingService(
        IObservableDirectoryWalkerFactory scanner,
        ILogger<DetectorProcessingService> logger)
    {
        this.scanner = scanner;
        this.logger = logger;
    }

    public async Task<DetectorProcessingResult> ProcessDetectorsAsync(IDetectionArguments detectionArguments, IEnumerable<IComponentDetector> detectors, DetectorRestrictions detectorRestrictions)
    {
        using var scope = this.logger.BeginScope("Processing detectors");
        this.logger.LogInformation($"Finding components...");

        var stopwatch = Stopwatch.StartNew();
        var exitCode = ProcessingResultCode.Success;

        // Run the scan on all protocol scanners and union the results
        var providerElapsedTime = new ConcurrentDictionary<string, DetectorRunResult>();
        var detectorArguments = GetDetectorArgs(detectionArguments.DetectorArgs);

        var exclusionPredicate = this.IsOSLinuxOrMac()
            ? this.GenerateDirectoryExclusionPredicate(detectionArguments.SourceDirectory.ToString(), detectionArguments.DirectoryExclusionList, detectionArguments.DirectoryExclusionListObsolete, allowWindowsPaths: false, ignoreCase: false)
            : this.GenerateDirectoryExclusionPredicate(detectionArguments.SourceDirectory.ToString(), detectionArguments.DirectoryExclusionList, detectionArguments.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);

        IEnumerable<Task<(IndividualDetectorScanResult, ComponentRecorder, IComponentDetector)>> scanTasks = detectors
            .Select(async detector =>
            {
                var providerStopwatch = new Stopwatch();
                providerStopwatch.Start();

                var componentRecorder = new ComponentRecorder(this.logger, !detector.NeedsAutomaticRootDependencyCalculation);

                var isExperimentalDetector = detector is IExperimentalDetector && !(detectorRestrictions.ExplicitlyEnabledDetectorIds?.Contains(detector.Id)).GetValueOrDefault();

                IEnumerable<DetectedComponent> detectedComponents;
                ProcessingResultCode resultCode;
                IEnumerable<ContainerDetails> containerDetails;
                IndividualDetectorScanResult result;
                using (var record = new DetectorExecutionTelemetryRecord())
                {
                    result = await this.WithExperimentalScanGuardsAsync(
                        () => detector.ExecuteDetectorAsync(new ScanRequest(detectionArguments.SourceDirectory, exclusionPredicate, this.logger, detectorArguments, detectionArguments.DockerImagesToScan, componentRecorder)),
                        isExperimentalDetector,
                        record);

                    // Make sure top level enumerables are at least empty and not null.
                    result = this.CoalesceResult(result);

                    detectedComponents = componentRecorder.GetDetectedComponents();
                    resultCode = result.ResultCode;
                    containerDetails = result.ContainerDetails;

                    record.AdditionalTelemetryDetails = result.AdditionalTelemetryDetails != null ? JsonConvert.SerializeObject(result.AdditionalTelemetryDetails) : null;
                    record.IsExperimental = isExperimentalDetector;
                    record.DetectorId = detector.Id;
                    record.DetectedComponentCount = detectedComponents.Count();
                    var dependencyGraphs = componentRecorder.GetDependencyGraphsByLocation().Values;
                    record.ExplicitlyReferencedComponentCount = dependencyGraphs.Select(dependencyGraph =>
                        {
                            return dependencyGraph.GetAllExplicitlyReferencedComponents();
                        })
                        .SelectMany(x => x)
                        .Distinct()
                        .Count();

                    record.ReturnCode = (int)resultCode;
                    record.StopExecutionTimer();
                    providerElapsedTime.TryAdd(detector.Id + (isExperimentalDetector ? " (Beta)" : string.Empty), new DetectorRunResult
                    {
                        ExecutionTime = record.ExecutionTime.Value,
                        ComponentsFoundCount = record.DetectedComponentCount.GetValueOrDefault(),
                        ExplicitlyReferencedComponentCount = record.ExplicitlyReferencedComponentCount.GetValueOrDefault(),
                        IsExperimental = isExperimentalDetector,
                    });
                }

                if (exitCode < resultCode && !isExperimentalDetector)
                {
                    exitCode = resultCode;
                }

                if (isExperimentalDetector)
                {
                    return (new IndividualDetectorScanResult(), new ComponentRecorder(), detector);
                }
                else
                {
                    return (result, componentRecorder, detector);
                }
            }).ToList();

        var results = await Task.WhenAll(scanTasks);

        var detectorProcessingResult = this.ConvertDetectorResultsIntoResult(results, exitCode);

        var totalElapsedTime = stopwatch.Elapsed.TotalSeconds;
        this.LogTabularOutput(this.logger, providerElapsedTime, totalElapsedTime);

        // If there are components which are skipped due to connection or parsing
        // errors, log them by detector.
        var parseWarningShown = false;
        foreach (var (_, recorder, detector) in results)
        {
            var skippedComponents = recorder.GetSkippedComponents();
            if (!skippedComponents.Any())
            {
                continue;
            }

            if (!parseWarningShown)
            {
                using var parseWarningScope = this.logger.BeginScope("Parse warnings");
                this.logger.LogWarning("Some components or files were not detected due to parsing failures or connectivity issues.");
                this.logger.LogWarning("Please review the logs above for more detailed information.");
                parseWarningShown = true;
            }

            using var scGroup = this.logger.BeginScope("Skipped Components");
            this.logger.LogWarning("Components skipped for {DetectorId} detector:", detector.Id);
            foreach (var component in skippedComponents)
            {
                this.logger.LogWarning("- {Component}", component);
            }
        }

        using var dtScope = this.logger.BeginScope("Detection Time");
        this.logger.LogInformation("Detection time: {DetectionTime} seconds.", totalElapsedTime);

        return detectorProcessingResult;
    }

    public ExcludeDirectoryPredicate GenerateDirectoryExclusionPredicate(string originalSourceDirectory, IEnumerable<string> directoryExclusionList, IEnumerable<string> directoryExclusionListObsolete, bool allowWindowsPaths, bool ignoreCase = true)
    {
        if (directoryExclusionListObsolete?.Any() != true && directoryExclusionList?.Any() != true)
        {
            return (ReadOnlySpan<char> nameOfDirectoryToConsider, ReadOnlySpan<char> pathOfParentOfDirectoryToConsider) => false;
        }

        if (directoryExclusionListObsolete?.Any() == true)
        {
            // Note: directory info will *automatically* parent relative paths to the working directory of the current assembly. Hold on to your rear.
            var directories = directoryExclusionListObsolete
                .Select(relativeOrAbsoluteExclusionPath => new DirectoryInfo(relativeOrAbsoluteExclusionPath))
                .Select(exclusionDirectoryInfo => new
                {
                    nameOfExcludedDirectory = exclusionDirectoryInfo.Name,
                    pathOfParentOfDirectoryToExclude = exclusionDirectoryInfo.Parent.FullName,
                    rootedLinuxSymlinkCompatibleRelativePathToExclude =
                        Path.GetDirectoryName(// Get the parent of
                            Path.IsPathRooted(exclusionDirectoryInfo.ToString())
                                ? exclusionDirectoryInfo.ToString() // If rooted, just use the natural path
                                : Path.Join(originalSourceDirectory, exclusionDirectoryInfo.ToString())), // If not rooted, join to sourceDir
                })
                .Distinct();

            return (ReadOnlySpan<char> nameOfDirectoryToConsiderSpan, ReadOnlySpan<char> pathOfParentOfDirectoryToConsiderSpan) =>
            {
                var pathOfParentOfDirectoryToConsider = pathOfParentOfDirectoryToConsiderSpan.ToString();
                var nameOfDirectoryToConsider = nameOfDirectoryToConsiderSpan.ToString();

                foreach (var valueTuple in directories)
                {
                    var nameOfExcludedDirectory = valueTuple.nameOfExcludedDirectory;
                    var pathOfParentOfDirectoryToExclude = valueTuple.pathOfParentOfDirectoryToExclude;

                    if (nameOfDirectoryToConsider.Equals(nameOfExcludedDirectory, StringComparison.Ordinal)
                        && (pathOfParentOfDirectoryToConsider.Equals(pathOfParentOfDirectoryToExclude, StringComparison.Ordinal)
                            || pathOfParentOfDirectoryToConsider.ToString().Equals(valueTuple.rootedLinuxSymlinkCompatibleRelativePathToExclude, StringComparison.Ordinal)))
                    {
                        this.logger.LogDebug("Excluding folder {Folder}.", Path.Combine(pathOfParentOfDirectoryToConsider, nameOfDirectoryToConsider));
                        return true;
                    }
                }

                return false;
            };
        }

        var minimatchers = new Dictionary<string, Glob>();

        var globOptions = new GlobOptions()
        {
            Evaluation = new EvaluationOptions()
            {
                CaseInsensitive = ignoreCase,
            },
        };

        foreach (var directoryExclusion in directoryExclusionList)
        {
            minimatchers.Add(directoryExclusion, Glob.Parse(allowWindowsPaths ? directoryExclusion : /* [] escapes special chars */ directoryExclusion.Replace("\\", "[\\]"), globOptions));
        }

        return (name, directoryName) =>
        {
            var path = Path.Combine(directoryName.ToString(), name.ToString());

            return minimatchers.Any(minimatcherKeyValue =>
            {
                if (minimatcherKeyValue.Value.IsMatch(path))
                {
                    this.logger.LogDebug("Excluding folder {Path} because it matched glob {Glob}.", path, minimatcherKeyValue.Key);
                    return true;
                }

                return false;
            });
        };
    }

    private static IDictionary<string, string> GetDetectorArgs(IEnumerable<string> detectorArgsList)
    {
        var detectorArgs = new Dictionary<string, string>();

        foreach (var arg in detectorArgsList)
        {
            var keyValue = arg.Split('=');

            if (keyValue.Length != 2)
            {
                continue;
            }

            detectorArgs.Add(keyValue[0], keyValue[1]);
        }

        return detectorArgs;
    }

    private IndividualDetectorScanResult CoalesceResult(IndividualDetectorScanResult individualDetectorScanResult)
    {
        individualDetectorScanResult ??= new IndividualDetectorScanResult();

        individualDetectorScanResult.ContainerDetails ??= Enumerable.Empty<ContainerDetails>();

        // Additional telemetry details can safely be null
        return individualDetectorScanResult;
    }

    private DetectorProcessingResult ConvertDetectorResultsIntoResult(IEnumerable<(IndividualDetectorScanResult Result, ComponentRecorder Recorder, IComponentDetector Detector)> results, ProcessingResultCode exitCode)
    {
        return new DetectorProcessingResult
        {
            ComponentRecorders = results.Select(tuple => (tuple.Detector, tuple.Recorder)),
            ContainersDetailsMap = results.SelectMany(x => x.Result.ContainerDetails).ToDictionary(x => x.Id),
            ResultCode = exitCode,
        };
    }

    private async Task<IndividualDetectorScanResult> WithExperimentalScanGuardsAsync(Func<Task<IndividualDetectorScanResult>> detectionTaskGenerator, bool isExperimentalDetector, DetectorExecutionTelemetryRecord telemetryRecord)
    {
        if (!isExperimentalDetector)
        {
            return await Task.Run(detectionTaskGenerator);
        }

        try
        {
            return await AsyncExecution.ExecuteWithTimeoutAsync(detectionTaskGenerator, TimeSpan.FromMinutes(4), CancellationToken.None);
        }
        catch (TimeoutException)
        {
            return new IndividualDetectorScanResult
            {
                ResultCode = ProcessingResultCode.TimeoutError,
            };
        }
        catch (Exception ex)
        {
            telemetryRecord.ExperimentalInformation = ex.ToString();
            return new IndividualDetectorScanResult
            {
                ResultCode = ProcessingResultCode.InputError,
            };
        }
    }

    private bool IsOSLinuxOrMac()
    {
        return OSVersion.Platform == PlatformID.MacOSX || OSVersion.Platform == PlatformID.Unix;
    }

    private void LogTabularOutput(ILogger logger, ConcurrentDictionary<string, DetectorRunResult> providerElapsedTime, double totalElapsedTime)
    {
        var tsf = new TabularStringFormat(new Column[]
        {
            new Column { Header = "Component Detector Id", Width = 30 },
            new Column { Header = "Detection Time", Width = 30, Format = "{0:g2} seconds" },
            new Column { Header = "# Components Found", Width = 30, },
            new Column { Header = "# Explicitly Referenced", Width = 40 },
        });

        var rows = providerElapsedTime.OrderBy(a => a.Key).Select(x =>
        {
            var componentResult = x.Value;
            return new object[]
            {
                x.Key,
                componentResult.ExecutionTime.TotalSeconds,
                componentResult.ComponentsFoundCount,
                componentResult.ExplicitlyReferencedComponentCount,
            };
        }).ToList();

        rows.Add(new object[]
        {
            "Total",
            totalElapsedTime,
            providerElapsedTime.Sum(x => x.Value.ComponentsFoundCount),
            providerElapsedTime.Sum(x => x.Value.ExplicitlyReferencedComponentCount),
        });

        foreach (var line in tsf.GenerateString(rows)
                     .Split(new string[] { NewLine }, StringSplitOptions.None))
        {
            this.logger.LogInformation("{Line}", line);
        }
    }
}

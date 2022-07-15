using DotNet.Globbing;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static System.Environment;

namespace Microsoft.ComponentDetection.Orchestrator.Services
{
    [Export(typeof(IDetectorProcessingService))]
    [Shared]
    public class DetectorProcessingService : ServiceBase, IDetectorProcessingService
    {
        /// <summary>
        /// Gets or sets the factory for handing back component streams to File detectors. Injected automatically by MEF composition.
        /// </summary>
        [Import]
        public IComponentStreamEnumerableFactory ComponentStreamEnumerableFactory { get; set; }

        [Import]
        public IObservableDirectoryWalkerFactory Scanner { get; set; }

        public async Task<DetectorProcessingResult> ProcessDetectorsAsync(IDetectionArguments detectionArguments, IEnumerable<IComponentDetector> detectors, DetectorRestrictions detectorRestrictions)
        {
            Logger.LogCreateLoggingGroup();
            Logger.LogInfo($"Finding components...");

            Stopwatch stopwatch = Stopwatch.StartNew();
            var exitCode = ProcessingResultCode.Success;

            // Run the scan on all protocol scanners and union the results
            ConcurrentDictionary<string, DetectorRunResult> providerElapsedTime = new ConcurrentDictionary<string, DetectorRunResult>();
            var detectorArguments = GetDetectorArgs(detectionArguments.DetectorArgs);

            ExcludeDirectoryPredicate exclusionPredicate = IsOSLinuxOrMac()
                ? GenerateDirectoryExclusionPredicate(detectionArguments.SourceDirectory.ToString(), detectionArguments.DirectoryExclusionList, detectionArguments.DirectoryExclusionListObsolete, allowWindowsPaths: false, ignoreCase: false)
                : GenerateDirectoryExclusionPredicate(detectionArguments.SourceDirectory.ToString(), detectionArguments.DirectoryExclusionList, detectionArguments.DirectoryExclusionListObsolete, allowWindowsPaths: true, ignoreCase: true);

            IEnumerable<Task<(IndividualDetectorScanResult, ComponentRecorder, IComponentDetector)>> scanTasks = detectors
                .Select(async detector =>
                {
                    Stopwatch providerStopwatch = new Stopwatch();
                    providerStopwatch.Start();

                    var componentRecorder = new ComponentRecorder(Logger, !detector.NeedsAutomaticRootDependencyCalculation);

                    var isExperimentalDetector = detector is IExperimentalDetector && !(detectorRestrictions.ExplicitlyEnabledDetectorIds?.Contains(detector.Id)).GetValueOrDefault();

                    IEnumerable<DetectedComponent> detectedComponents;
                    ProcessingResultCode resultCode;
                    IEnumerable<ContainerDetails> containerDetails;
                    IndividualDetectorScanResult result;
                    using (var record = new DetectorExecutionTelemetryRecord())
                    {
                        result = await WithExperimentalScanGuards(
                            () => detector.ExecuteDetectorAsync(new ScanRequest(detectionArguments.SourceDirectory, exclusionPredicate, Logger, detectorArguments, detectionArguments.DockerImagesToScan, componentRecorder)),
                            isExperimentalDetector,
                            record);

                        // Make sure top level enumerables are at least empty and not null.
                        result = CoalesceResult(result);

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

            DetectorProcessingResult detectorProcessingResult = ConvertDetectorResultsIntoResult(results, exitCode);

            var totalElapsedTime = stopwatch.Elapsed.TotalSeconds;
            LogTabularOutput(Logger, providerElapsedTime, totalElapsedTime);

            Logger.LogCreateLoggingGroup();
            Logger.LogInfo($"Detection time: {totalElapsedTime} seconds.");

            return detectorProcessingResult;
        }

        private IndividualDetectorScanResult CoalesceResult(IndividualDetectorScanResult individualDetectorScanResult)
        {
            if (individualDetectorScanResult == null)
            {
                individualDetectorScanResult = new IndividualDetectorScanResult();
            }

            if (individualDetectorScanResult.ContainerDetails == null)
            {
                individualDetectorScanResult.ContainerDetails = Enumerable.Empty<ContainerDetails>();
            }

            // Additional telemetry details can safely be null
            return individualDetectorScanResult;
        }

        private DetectorProcessingResult ConvertDetectorResultsIntoResult(IEnumerable<(IndividualDetectorScanResult result, ComponentRecorder recorder, IComponentDetector detector)> results, ProcessingResultCode exitCode)
        {
            return new DetectorProcessingResult
            {
                ComponentRecorders = results.Select(tuple => (tuple.detector, tuple.recorder)),
                ContainersDetailsMap = results.SelectMany(x => x.result.ContainerDetails).ToDictionary(x => x.Id),
                ResultCode = exitCode,
            };
        }

        private async Task<IndividualDetectorScanResult> WithExperimentalScanGuards(Func<Task<IndividualDetectorScanResult>> detectionTaskGenerator, bool isExperimentalDetector, DetectorExecutionTelemetryRecord telemetryRecord)
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
            TabularStringFormat tsf = new TabularStringFormat(new Column[]
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
                                    .Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
            {
                Logger.LogInfo(line);
            }
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

        public ExcludeDirectoryPredicate GenerateDirectoryExclusionPredicate(string originalSourceDirectory, IEnumerable<string> directoryExclusionList, IEnumerable<string> directoryExclusionListObsolete, bool allowWindowsPaths, bool ignoreCase = true)
        {
            if (directoryExclusionListObsolete?.Any() != true && directoryExclusionList?.Any() != true)
            {
                return (ReadOnlySpan<char> nameOfDirectoryToConsider, ReadOnlySpan<char> pathOfParentOfDirectoryToConsider) => false;
            }

            if (directoryExclusionListObsolete?.Any() == true)
            {
                var directories = directoryExclusionListObsolete

                    // Note: directory info will *automatically* parent relative paths to the working directory of the current assembly. Hold on to your rear.
                    .Select(relativeOrAbsoluteExclusionPath => new DirectoryInfo(relativeOrAbsoluteExclusionPath))
                    .Select(exclusionDirectoryInfo => new
                    {
                        nameOfExcludedDirectory = exclusionDirectoryInfo.Name,
                        pathOfParentOfDirectoryToExclude = exclusionDirectoryInfo.Parent.FullName,
                        rootedLinuxSymlinkCompatibleRelativePathToExclude =
                            Path.GetDirectoryName( // Get the parent of
                                Path.IsPathRooted(exclusionDirectoryInfo.ToString())
                                ? exclusionDirectoryInfo.ToString() // If rooted, just use the natural path
                                : Path.Join(originalSourceDirectory, exclusionDirectoryInfo.ToString())), // If not rooted, join to sourceDir
                    })
                    .Distinct();

                return (ReadOnlySpan<char> nameOfDirectoryToConsiderSpan, ReadOnlySpan<char> pathOfParentOfDirectoryToConsiderSpan) =>
                {
                    string pathOfParentOfDirectoryToConsider = pathOfParentOfDirectoryToConsiderSpan.ToString();
                    string nameOfDirectoryToConsider = nameOfDirectoryToConsiderSpan.ToString();

                    foreach (var valueTuple in directories)
                    {
                        var nameOfExcludedDirectory = valueTuple.nameOfExcludedDirectory;
                        var pathOfParentOfDirectoryToExclude = valueTuple.pathOfParentOfDirectoryToExclude;

                        if (nameOfDirectoryToConsider.Equals(nameOfExcludedDirectory, StringComparison.Ordinal)
                        && (pathOfParentOfDirectoryToConsider.Equals(pathOfParentOfDirectoryToExclude, StringComparison.Ordinal)
                            || pathOfParentOfDirectoryToConsider.ToString().Equals(valueTuple.rootedLinuxSymlinkCompatibleRelativePathToExclude, StringComparison.Ordinal)))
                        {
                            Logger.LogVerbose($"Excluding folder {Path.Combine(pathOfParentOfDirectoryToConsider.ToString(), nameOfDirectoryToConsider.ToString())}.");
                            return true;
                        }
                    }

                    return false;
                };
            }

            Dictionary<string, Glob> minimatchers = new Dictionary<string, Glob>();

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
                        Logger.LogVerbose($"Excluding folder {path} because it matched glob {minimatcherKeyValue.Key}.");
                        return true;
                    }

                    return false;
                });
            };
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.ComponentDetection.Detectors.DotNet;
using Microsoft.Extensions.Logging;

/// <summary>
/// Processes MSBuild binary log files to extract project information.
/// </summary>
internal class BinLogProcessor : IBinLogProcessor
{
    private readonly Microsoft.Extensions.Logging.ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinLogProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public BinLogProcessor(Microsoft.Extensions.Logging.ILogger logger) => this.logger = logger;

    /// <inheritdoc />
    public IReadOnlyList<MSBuildProjectInfo> ExtractProjectInfo(string binlogPath, string? sourceDirectory = null)
    {
        // Maps project path to the primary MSBuildProjectInfo for that project
        var projectInfoByPath = new Dictionary<string, MSBuildProjectInfo>(StringComparer.OrdinalIgnoreCase);

        // Pre-compute source directory info for path rebasing.
        // When the binlog was built on a different machine, BinLogFilePath (recorded at the start
        // of the log) lets us derive the root substitution. The rebasePath function is set once
        // the BinLogFilePath message is seen and is applied inline to all path-valued properties.
        var normalizedSourceDir = PathRebasingUtility.NormalizeDirectory(sourceDirectory);
        var binlogDir = PathRebasingUtility.NormalizeDirectory(Path.GetDirectoryName(binlogPath));
        Func<string, string>? rebasePath = null;

        try
        {
            var reader = new BinLogReader();

            reader.OnException += ex =>
                this.logger.LogWarning(ex, "BinLogReader.OnException during replay");

            reader.RecoverableReadError += args =>
                this.logger.LogDebug("BinLogReader.RecoverableReadError: {Message}", args.ErrorType);

            // Maps evaluation ID to MSBuildProjectInfo being populated
            var projectInfoByEvaluationId = new Dictionary<int, MSBuildProjectInfo>();

            // Maps project instance ID to evaluation ID
            var projectInstanceToEvaluationMap = new Dictionary<int, int>();

            // Diagnostic counters
            var statusEventCount = 0;
            var evalFinishedCount = 0;
            var projectStartedCount = 0;
            var projectStartedSkippedCount = 0;
            var projectFinishedCount = 0;
            var projectFinishedAddedCount = 0;
            var anyEventCount = 0;
            var firstEventTypes = new List<string>(10);

            // Hook into status events to capture property evaluations
            reader.StatusEventRaised += (sender, e) =>
            {
                statusEventCount++;
                if (statusEventCount <= 3)
                {
                    this.logger.LogDebug(
                        "StatusEvent[{Index}]: Type={Type}, EvalId={EvalId}",
                        statusEventCount,
                        e?.GetType().FullName,
                        e?.BuildEventContext?.EvaluationId);
                }

                if (e?.BuildEventContext?.EvaluationId >= 0 &&
                    e is ProjectEvaluationFinishedEventArgs projectEvalArgs)
                {
                    evalFinishedCount++;

                    // Reuse existing project info if one was created during evaluation
                    // (e.g., from EnvironmentVariableReadEventArgs or PropertyInitialValueSetEventArgs)
                    if (!projectInfoByEvaluationId.TryGetValue(e.BuildEventContext.EvaluationId, out var projectInfo))
                    {
                        projectInfo = new MSBuildProjectInfo();
                        projectInfoByEvaluationId[e.BuildEventContext.EvaluationId] = projectInfo;
                    }

                    this.PopulateFromEvaluation(projectEvalArgs, projectInfo, rebasePath);
                }
            };

            // Hook into project started to map project instance to evaluation and capture project path
            reader.ProjectStarted += (sender, e) =>
            {
                projectStartedCount++;
                if (e?.BuildEventContext?.EvaluationId >= 0 &&
                    e?.BuildEventContext?.ProjectInstanceId >= 0)
                {
                    projectInstanceToEvaluationMap[e.BuildEventContext.ProjectInstanceId] = e.BuildEventContext.EvaluationId;

                    // Set the project path on the MSBuildProjectInfo
                    if (!string.IsNullOrEmpty(e.ProjectFile) &&
                        projectInfoByEvaluationId.TryGetValue(e.BuildEventContext.EvaluationId, out var projectInfo))
                    {
                        projectInfo.ProjectPath = rebasePath != null ? rebasePath(e.ProjectFile) : e.ProjectFile;
                    }
                }
                else
                {
                    projectStartedSkippedCount++;
                    this.logger.LogDebug(
                        "ProjectStarted skipped: EvalId={EvalId}, InstanceId={InstanceId}, File={File}",
                        e?.BuildEventContext?.EvaluationId,
                        e?.BuildEventContext?.ProjectInstanceId,
                        e?.ProjectFile);
                }
            };

            // Hook into message events to capture BinLogFilePath from the initial build messages.
            // MSBuild's BinaryLogger writes "BinLogFilePath=<path>" as a BuildMessageEventArgs
            // with SenderName "BinaryLogger" at the start of the log. This arrives before any
            // evaluation or project events, so we can compute the rebase function here and apply
            // it to all subsequent path-valued properties.
            // https://github.com/dotnet/msbuild/blob/7d73e8e9074fe9a4420e38cd22d45645b28a11f7/src/Build/Logging/BinaryLogger/BinaryLogger.cs#L473
            reader.MessageRaised += (sender, e) =>
            {
                if (rebasePath == null &&
                    binlogDir != null &&
                    normalizedSourceDir != null &&
                    e is BuildMessageEventArgs msg &&
                    msg.SenderName == "BinaryLogger" &&
                    msg.Message != null &&
                    msg.Message.StartsWith("BinLogFilePath=", StringComparison.Ordinal))
                {
                    var originalBinLogFilePath = msg.Message["BinLogFilePath=".Length..];
                    var originalBinlogDir = PathRebasingUtility.NormalizeDirectory(Path.GetDirectoryName(originalBinLogFilePath));
                    var rebaseRoot = PathRebasingUtility.GetRebaseRoot(normalizedSourceDir, binlogDir, originalBinlogDir);
                    if (rebaseRoot != null)
                    {
                        this.logger.LogDebug(
                            "Rebasing binlog paths from build-machine root '{RebaseRoot}' to scan-machine root '{SourceDirectory}'",
                            rebaseRoot,
                            normalizedSourceDir);
                        rebasePath = path => PathRebasingUtility.RebasePath(path, rebaseRoot, normalizedSourceDir!);
                    }
                }
            };

            // Hook into any event to capture property reassignments and item changes during build
            reader.AnyEventRaised += (sender, e) =>
            {
                anyEventCount++;
                if (firstEventTypes.Count < 10)
                {
                    firstEventTypes.Add(e?.GetType().Name ?? "null");
                }

                this.HandleBuildEvent(e, projectInstanceToEvaluationMap, projectInfoByEvaluationId, rebasePath);
            };

            // Hook into project finished to collect final project info and establish hierarchy
            reader.ProjectFinished += (sender, e) =>
            {
                projectFinishedCount++;
                if (e?.BuildEventContext?.ProjectInstanceId >= 0 &&
                    projectInstanceToEvaluationMap.TryGetValue(e.BuildEventContext.ProjectInstanceId, out var evaluationId) &&
                    projectInfoByEvaluationId.TryGetValue(evaluationId, out var projectInfo) &&
                    !string.IsNullOrEmpty(projectInfo.ProjectPath))
                {
                    projectFinishedAddedCount++;
                    this.AddOrMergeProjectInfo(projectInfo, projectInfoByPath);
                }
            };

            reader.Replay(binlogPath);

            this.logger.LogDebug(
                "Binlog replay complete: StatusEvents={StatusEvents}, EvalFinished={EvalFinished}, " +
                "ProjectStarted={ProjectStarted} (skipped={ProjectStartedSkipped}), " +
                "ProjectFinished={ProjectFinished} (added={ProjectFinishedAdded}), " +
                "AnyEvents={AnyEvents}, EvalMapSize={EvalMapSize}, InstanceMapSize={InstanceMapSize}, Results={Results}, " +
                "FirstEventTypes=[{FirstEventTypes}]",
                statusEventCount,
                evalFinishedCount,
                projectStartedCount,
                projectStartedSkippedCount,
                projectFinishedCount,
                projectFinishedAddedCount,
                anyEventCount,
                projectInfoByEvaluationId.Count,
                projectInstanceToEvaluationMap.Count,
                projectInfoByPath.Count,
                string.Join(", ", firstEventTypes));
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Error parsing binlog: {BinlogPath}", binlogPath);
        }

        return [.. projectInfoByPath.Values];
    }

    /// <summary>
    /// Adds a project info to the results, merging with existing entries for the same project path.
    /// Outer builds become the primary entry; inner builds are added as children.
    /// </summary>
    private void AddOrMergeProjectInfo(
        MSBuildProjectInfo projectInfo,
        Dictionary<string, MSBuildProjectInfo> projectInfoByPath)
    {
        if (!projectInfoByPath.TryGetValue(projectInfo.ProjectPath!, out var existing))
        {
            // First time seeing this project - add it
            projectInfoByPath[projectInfo.ProjectPath!] = projectInfo;
            return;
        }

        // We've seen this project before - determine how to merge
        if (projectInfo.IsOuterBuild && !existing.IsOuterBuild)
        {
            // New build is outer, existing is inner - outer becomes primary
            // Move existing to be an inner build of the new outer build
            projectInfo.InnerBuilds.Add(existing);

            // Also move any inner builds that were already collected
            foreach (var inner in existing.InnerBuilds)
            {
                projectInfo.InnerBuilds.Add(inner);
            }

            existing.InnerBuilds.Clear();

            // Replace in the lookup
            projectInfoByPath[projectInfo.ProjectPath!] = projectInfo;
        }
        else if (existing.IsOuterBuild && !projectInfo.IsOuterBuild && !string.IsNullOrEmpty(projectInfo.TargetFramework))
        {
            // Existing is outer, new is inner - de-duplicate by TargetFramework
            var matchingInner = existing.InnerBuilds.FirstOrDefault(
                ib => string.Equals(ib.TargetFramework, projectInfo.TargetFramework, StringComparison.OrdinalIgnoreCase));
            if (matchingInner != null)
            {
                // Same TFM seen again (e.g., build + publish pass) - merge
                matchingInner.MergeWith(projectInfo);
            }
            else
            {
                existing.InnerBuilds.Add(projectInfo);
            }
        }
        else if (!existing.IsOuterBuild && !projectInfo.IsOuterBuild && !string.IsNullOrEmpty(projectInfo.TargetFramework))
        {
            // Both are single-TFM builds - check if they share the same TFM
            if (string.Equals(existing.TargetFramework, projectInfo.TargetFramework, StringComparison.OrdinalIgnoreCase))
            {
                // Same project, same TFM (e.g. build + publish) - merge as superset
                existing.MergeWith(projectInfo);
            }
            else
            {
                // Different TFMs (no outer build seen yet) - add to InnerBuilds of the first one
                // The first one acts as a placeholder until we see an outer build
                existing.InnerBuilds.Add(projectInfo);
            }
        }
        else if (existing.IsOuterBuild && projectInfo.IsOuterBuild)
        {
            // Both are outer builds (e.g. build + publish of a multi-targeted project)
            // Merge inner builds: for matching TFMs, merge; for new TFMs, add
            foreach (var newInner in projectInfo.InnerBuilds)
            {
                var matchingInner = existing.InnerBuilds.FirstOrDefault(
                    ib => string.Equals(ib.TargetFramework, newInner.TargetFramework, StringComparison.OrdinalIgnoreCase));
                if (matchingInner != null)
                {
                    matchingInner.MergeWith(newInner);
                }
                else
                {
                    existing.InnerBuilds.Add(newInner);
                }
            }

            // Merge the outer build properties/items too
            existing.MergeWith(projectInfo);
        }
        else
        {
            // Fallback: merge properties/items as superset
            existing.MergeWith(projectInfo);
        }
    }

    /// <summary>
    /// Populates project info from evaluation results (properties and items).
    /// </summary>
    private void PopulateFromEvaluation(ProjectEvaluationFinishedEventArgs projectEvalArgs, MSBuildProjectInfo projectInfo, Func<string, string>? rebasePath)
    {
        // Extract properties
        if (projectEvalArgs?.Properties != null)
        {
            // Handle different property collection types based on MSBuild version
            // Newer MSBuild versions may provide IDictionary<string, string>
            if (projectEvalArgs.Properties is IDictionary<string, string> propertiesDict)
            {
                foreach (var kvp in propertiesDict)
                {
                    this.SetPropertyWithRebase(projectInfo, kvp.Key, kvp.Value, rebasePath);
                }
            }
            else
            {
                // Older format uses IEnumerable with DictionaryEntry or KeyValuePair
                foreach (var property in projectEvalArgs.Properties)
                {
                    string? key = null;
                    string? value = null;

                    if (property is DictionaryEntry entry)
                    {
                        key = entry.Key as string;
                        value = entry.Value as string;
                    }
                    else if (property is KeyValuePair<string, string> kvp)
                    {
                        key = kvp.Key;
                        value = kvp.Value;
                    }

                    if (!string.IsNullOrEmpty(key))
                    {
                        this.SetPropertyWithRebase(projectInfo, key, value ?? string.Empty, rebasePath);
                    }
                }
            }
        }

        // Extract items
        if (projectEvalArgs?.Items != null)
        {
            // Items is a flat IList<DictionaryEntry> where each entry has:
            //   Key = item type (string, e.g., "PackageReference")
            //   Value = single ITaskItem (TaskItemData from binlog deserialization)
            foreach (var itemEntry in projectEvalArgs.Items)
            {
                if (itemEntry is DictionaryEntry entry &&
                    entry.Key is string itemType &&
                    MSBuildProjectInfo.IsItemTypeOfInterest(itemType, out var isPath) &&
                    entry.Value is ITaskItem taskItem)
                {
                    if (isPath && rebasePath != null)
                    {
                        // Rebase the item spec if it's a path
                        taskItem.ItemSpec = rebasePath(taskItem.ItemSpec);
                    }

                    projectInfo.TryAddOrUpdateItem(itemType, taskItem);
                }
            }
        }
    }

    /// <summary>
    /// Handles build events to capture property and item changes during target execution.
    /// </summary>
    private void HandleBuildEvent(
        BuildEventArgs? args,
        Dictionary<int, int> projectInstanceToEvaluationMap,
        Dictionary<int, MSBuildProjectInfo> projectInfoByEvaluationId,
        Func<string, string>? rebasePath)
    {
        if (!this.TryGetProjectInfo(args, projectInstanceToEvaluationMap, projectInfoByEvaluationId, out var projectInfo))
        {
            return;
        }

        switch (args)
        {
            // Property reassignments (when a property value changes during the build)
            case PropertyReassignmentEventArgs propertyReassignment:
                this.SetPropertyWithRebase(projectInfo, propertyReassignment.PropertyName, propertyReassignment.NewValue, rebasePath);
                break;

            // Initial property value set events
            case PropertyInitialValueSetEventArgs propertyInitialValueSet:
                this.SetPropertyWithRebase(projectInfo, propertyInitialValueSet.PropertyName, propertyInitialValueSet.PropertyValue, rebasePath);
                break;

            // Environment variable reads during evaluation - MSBuild promotes env vars to properties
            case EnvironmentVariableReadEventArgs envVarRead when
                !string.IsNullOrEmpty(envVarRead.EnvironmentVariableName):
                this.SetPropertyWithRebase(projectInfo, envVarRead.EnvironmentVariableName, envVarRead.Message ?? string.Empty, rebasePath);
                break;

            // Task parameter events which can contain item arrays for add/remove/update
            case TaskParameterEventArgs taskParameter when
                taskParameter.Items is IList<ITaskItem> taskItems:
                this.ProcessTaskParameterItems(taskParameter.Kind, taskParameter.ItemType, taskItems, projectInfo, rebasePath);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Tries to get the project info for a build event.
    /// </summary>
    private bool TryGetProjectInfo(
        BuildEventArgs? args,
        Dictionary<int, int> projectInstanceToEvaluationMap,
        Dictionary<int, MSBuildProjectInfo> projectInfoByEvaluationId,
        out MSBuildProjectInfo projectInfo)
    {
        projectInfo = null!;

        // Try ProjectInstanceId first (available during build/target execution)
        if (args?.BuildEventContext?.ProjectInstanceId >= 0 &&
            projectInstanceToEvaluationMap.TryGetValue(args.BuildEventContext.ProjectInstanceId, out var evaluationId) &&
            projectInfoByEvaluationId.TryGetValue(evaluationId, out projectInfo!))
        {
            return true;
        }

        // Fall back to EvaluationId (available during evaluation, before ProjectStarted)
        if (args?.BuildEventContext?.EvaluationId >= 0)
        {
            if (!projectInfoByEvaluationId.TryGetValue(args.BuildEventContext.EvaluationId, out projectInfo!))
            {
                // Create lazily for evaluation-time events that fire before ProjectEvaluationFinished
                projectInfo = new MSBuildProjectInfo();
                projectInfoByEvaluationId[args.BuildEventContext.EvaluationId] = projectInfo;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Processes task parameter items for add/remove operations.
    /// </summary>
    private void ProcessTaskParameterItems(
        TaskParameterMessageKind kind,
        string itemType,
        IList<ITaskItem> items,
        MSBuildProjectInfo projectInfo,
        Func<string, string>? rebasePath)
    {
        if (!MSBuildProjectInfo.IsItemTypeOfInterest(itemType, out var isPath))
        {
            return;
        }

        if (kind == TaskParameterMessageKind.RemoveItem)
        {
            foreach (var item in items)
            {
                var itemSpec = isPath && rebasePath != null ? rebasePath(item.ItemSpec) : item.ItemSpec;
                projectInfo.TryRemoveItem(itemType, itemSpec);
            }
        }
        else if (kind == TaskParameterMessageKind.TaskInput ||
                 kind == TaskParameterMessageKind.AddItem ||
                 kind == TaskParameterMessageKind.TaskOutput)
        {
            foreach (var item in items)
            {
                if (isPath && rebasePath != null)
                {
                    // Rebase the item spec if it's a path
                    item.ItemSpec = rebasePath(item.ItemSpec);
                }

                projectInfo.TryAddOrUpdateItem(itemType, item);
            }
        }

        // SkippedTargetInputs and SkippedTargetOutputs are informational and don't modify items
    }

    /// <summary>
    /// Sets a property on a project info, rebasing the value first if it is a path property.
    /// </summary>
    private void SetPropertyWithRebase(MSBuildProjectInfo projectInfo, string propertyName, string value, Func<string, string>? rebasePath)
    {
        if (!MSBuildProjectInfo.IsPropertyOfInterest(propertyName, out var isPath))
        {
            return;
        }

        if (isPath && rebasePath != null && !string.IsNullOrEmpty(value))
        {
            value = rebasePath(value);
        }

        projectInfo.TrySetProperty(propertyName, value);
    }
}

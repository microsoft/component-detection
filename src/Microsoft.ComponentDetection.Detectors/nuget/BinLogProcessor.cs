namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
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
    public IReadOnlyList<MSBuildProjectInfo> ExtractProjectInfo(string binlogPath)
    {
        // Maps project path to the primary MSBuildProjectInfo for that project
        var projectInfoByPath = new Dictionary<string, MSBuildProjectInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var reader = new BinLogReader();

            // Maps evaluation ID to MSBuildProjectInfo being populated
            var projectInfoByEvaluationId = new Dictionary<int, MSBuildProjectInfo>();

            // Maps project instance ID to evaluation ID
            var projectInstanceToEvaluationMap = new Dictionary<int, int>();

            // Hook into status events to capture property evaluations
            reader.StatusEventRaised += (sender, e) =>
            {
                if (e?.BuildEventContext?.EvaluationId >= 0 &&
                    e is ProjectEvaluationFinishedEventArgs projectEvalArgs)
                {
                    var projectInfo = new MSBuildProjectInfo();
                    this.PopulateFromEvaluation(projectEvalArgs, projectInfo);
                    projectInfoByEvaluationId[e.BuildEventContext.EvaluationId] = projectInfo;
                }
            };

            // Hook into project started to map project instance to evaluation and capture project path
            reader.ProjectStarted += (sender, e) =>
            {
                if (e?.BuildEventContext?.EvaluationId >= 0 &&
                    e?.BuildEventContext?.ProjectInstanceId >= 0)
                {
                    projectInstanceToEvaluationMap[e.BuildEventContext.ProjectInstanceId] = e.BuildEventContext.EvaluationId;

                    // Set the project path on the MSBuildProjectInfo
                    if (!string.IsNullOrEmpty(e.ProjectFile) &&
                        projectInfoByEvaluationId.TryGetValue(e.BuildEventContext.EvaluationId, out var projectInfo))
                    {
                        projectInfo.ProjectPath = e.ProjectFile;
                    }
                }
            };

            // Hook into any event to capture property reassignments and item changes during build
            reader.AnyEventRaised += (sender, e) =>
            {
                this.HandleBuildEvent(e, projectInstanceToEvaluationMap, projectInfoByEvaluationId);
            };

            // Hook into project finished to collect final project info and establish hierarchy
            reader.ProjectFinished += (sender, e) =>
            {
                if (e?.BuildEventContext?.ProjectInstanceId >= 0 &&
                    projectInstanceToEvaluationMap.TryGetValue(e.BuildEventContext.ProjectInstanceId, out var evaluationId) &&
                    projectInfoByEvaluationId.TryGetValue(evaluationId, out var projectInfo) &&
                    !string.IsNullOrEmpty(projectInfo.ProjectPath))
                {
                    this.AddOrMergeProjectInfo(projectInfo, projectInfoByPath);
                }
            };

            reader.Replay(binlogPath);
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
            // Existing is outer, new is inner - add new as inner build
            existing.InnerBuilds.Add(projectInfo);
        }
        else if (!existing.IsOuterBuild && !projectInfo.IsOuterBuild && !string.IsNullOrEmpty(projectInfo.TargetFramework))
        {
            // Both are inner builds (no outer build seen yet) - add to InnerBuilds of the first one
            // The first one acts as a placeholder until we see an outer build
            existing.InnerBuilds.Add(projectInfo);
        }

        // Otherwise: duplicate builds currently ignored.
    }

    /// <summary>
    /// Populates project info from evaluation results (properties and items).
    /// </summary>
    private void PopulateFromEvaluation(ProjectEvaluationFinishedEventArgs projectEvalArgs, MSBuildProjectInfo projectInfo)
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
                    projectInfo.TrySetProperty(kvp.Key, kvp.Value);
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
                        projectInfo.TrySetProperty(key, value ?? string.Empty);
                    }
                }
            }
        }

        // Extract items
        if (projectEvalArgs?.Items != null)
        {
            // Items is an IEnumerable that contains item groups
            // Each item group has an ItemType (Key) and Items collection (Value)
            foreach (var itemGroup in projectEvalArgs.Items)
            {
                if (itemGroup is DictionaryEntry entry &&
                    entry.Key is string itemType &&
                    MSBuildProjectInfo.IsItemTypeOfInterest(itemType) &&
                    entry.Value is IEnumerable<object> groupItems)
                {
                    foreach (var item in groupItems)
                    {
                        if (item is ITaskItem taskItem)
                        {
                            projectInfo.TryAddOrUpdateItem(itemType, taskItem);
                        }
                    }
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
        Dictionary<int, MSBuildProjectInfo> projectInfoByEvaluationId)
    {
        if (!this.TryGetProjectInfo(args, projectInstanceToEvaluationMap, projectInfoByEvaluationId, out var projectInfo))
        {
            return;
        }

        switch (args)
        {
            // Property reassignments (when a property value changes during the build)
            case PropertyReassignmentEventArgs propertyReassignment:
                projectInfo.TrySetProperty(propertyReassignment.PropertyName, propertyReassignment.NewValue);
                break;

            // Initial property value set events
            case PropertyInitialValueSetEventArgs propertyInitialValueSet:
                projectInfo.TrySetProperty(propertyInitialValueSet.PropertyName, propertyInitialValueSet.PropertyValue);
                break;

            // Task parameter events which can contain item arrays for add/remove/update
            case TaskParameterEventArgs taskParameter when
                MSBuildProjectInfo.IsItemTypeOfInterest(taskParameter.ItemType) &&
                taskParameter.Items is IList<ITaskItem> taskItems:
                this.ProcessTaskParameterItems(taskParameter.Kind, taskParameter.ItemType, taskItems, projectInfo);
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

        if (args?.BuildEventContext?.ProjectInstanceId == null || args.BuildEventContext.ProjectInstanceId < 0)
        {
            return false;
        }

        if (!projectInstanceToEvaluationMap.TryGetValue(args.BuildEventContext.ProjectInstanceId, out var evaluationId) ||
            !projectInfoByEvaluationId.TryGetValue(evaluationId, out projectInfo!))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Processes task parameter items for add/remove operations.
    /// </summary>
    private void ProcessTaskParameterItems(
        TaskParameterMessageKind kind,
        string itemType,
        IList<ITaskItem> items,
        MSBuildProjectInfo projectInfo)
    {
        if (kind == TaskParameterMessageKind.RemoveItem)
        {
            foreach (var item in items)
            {
                projectInfo.TryRemoveItem(itemType, item.ItemSpec);
            }
        }
        else if (kind == TaskParameterMessageKind.TaskInput ||
                 kind == TaskParameterMessageKind.AddItem ||
                 kind == TaskParameterMessageKind.TaskOutput)
        {
            foreach (var item in items)
            {
                projectInfo.TryAddOrUpdateItem(itemType, item);
            }
        }

        // SkippedTargetInputs and SkippedTargetOutputs are informational and don't modify items
    }
}

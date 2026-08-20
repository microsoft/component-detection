#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public static class GoDependencyGraphUtility
{
    public static async Task<bool> GenerateAndPopulateDependencyGraphAsync(
        ICommandLineInvocationService commandLineInvocationService,
        ILogger logger,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        string projectRootDirectory,
        GoGraphTelemetryRecord record,
        CancellationToken cancellationToken = default)
    {
        var generateGraphProcess = await commandLineInvocationService.ExecuteCommandAsync("go", null, workingDirectory: new DirectoryInfo(projectRootDirectory), cancellationToken, new List<string> { "mod", "graph" }.ToArray());
        if (generateGraphProcess.ExitCode == 0)
        {
            PopulateDependencyGraph(generateGraphProcess.StdOut, singleFileComponentRecorder, logger);
            record.WasGraphSuccessful = true;
            return true;
        }

        return false;
    }

    private static void PopulateDependencyGraph(string goGraphOutput, ISingleFileComponentRecorder componentRecorder, ILogger logger)
    {
        var graphRelationships = goGraphOutput.Split('\n');

        foreach (var relationship in graphRelationships)
        {
            var components = relationship.Split(' ');
            if (components.Length != 2)
            {
                if (string.IsNullOrWhiteSpace(relationship))
                {
                    continue;
                }

                logger.LogWarning("Unexpected relationship output from go mod graph: {Relationship}", relationship);
                continue;
            }

            var isParentParsed = TryCreateGoComponentFromRelationshipPart(components[0], out var parentComponent);
            var isChildParsed = TryCreateGoComponentFromRelationshipPart(components[1], out var childComponent);

            if (!isParentParsed)
            {
                continue;
            }

            if (isChildParsed)
            {
                if (IsModuleInBuildList(componentRecorder, parentComponent) && IsModuleInBuildList(componentRecorder, childComponent))
                {
                    componentRecorder.RegisterUsage(new DetectedComponent(childComponent), parentComponentId: parentComponent.Id);
                }
            }
            else
            {
                logger.LogWarning("Failed to parse components from relationship string {Relationship}", relationship);
                componentRecorder.RegisterPackageParseFailure(relationship);
            }
        }
    }

    private static bool IsModuleInBuildList(ISingleFileComponentRecorder singleFileComponentRecorder, GoComponent component)
    {
        return singleFileComponentRecorder.GetComponent(component.Id) != null;
    }

    private static bool TryCreateGoComponentFromRelationshipPart(string relationship, out GoComponent goComponent)
    {
        var componentParts = relationship.Split('@');
        if (componentParts.Length != 2)
        {
            goComponent = null;
            return false;
        }

        goComponent = new GoComponent(componentParts[0], componentParts[1]);
        return true;
    }
}

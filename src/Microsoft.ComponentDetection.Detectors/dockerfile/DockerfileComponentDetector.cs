#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Dockerfile;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Valleysoft.DockerfileModel;

public class DockerfileComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IEnvironmentVariableService envVarService;

    public DockerfileComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IEnvironmentVariableService envVarService,
        ILogger<DockerfileComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.commandLineInvocationService = commandLineInvocationService;
        this.envVarService = envVarService;
        this.Logger = logger;
    }

    public override string Id { get; } = "DockerReference";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.DockerReference)];

    public override IList<string> SearchPatterns { get; } = ["dockerfile", "dockerfile.*", "*.dockerfile"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.DockerReference];

    public override int Version => 1;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;
        var filePath = file.Location;
        try
        {
            this.Logger.LogInformation("Discovered dockerfile: {Location}", file.Location);

            string contents;
            using (var reader = new StreamReader(file.Stream))
            {
                contents = await reader.ReadToEndAsync(cancellationToken);
            }

            var stageNameMap = new Dictionary<string, string>();
            var dockerFileComponent = this.ParseDockerFileAsync(contents, file.Location, singleFileComponentRecorder, stageNameMap);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "The file doesn't appear to be a Dockerfile: {Location}", filePath);
        }
    }

    private Task ParseDockerFileAsync(string fileContents, string fileLocation, ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, string> stageNameMap)
    {
        var dockerfileModel = Dockerfile.Parse(fileContents);
        var instructions = dockerfileModel.Items;
        foreach (var instruction in instructions)
        {
            var imageReference = this.ProcessDockerfileConstruct(instruction, dockerfileModel.EscapeChar, stageNameMap);
            if (imageReference != null)
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(imageReference.ToTypedDockerReferenceComponent()));
            }
        }

        return Task.CompletedTask;
    }

    private DockerReference ProcessDockerfileConstruct(DockerfileConstruct construct, char escapeChar, Dictionary<string, string> stageNameMap)
    {
        try
        {
            var instructionKeyword = construct.Type;
            DockerReference baseImage = null;
            if (instructionKeyword == ConstructType.Instruction)
            {
                var constructType = construct.GetType().Name;
                switch (constructType)
                {
                    case "FromInstruction":
                        baseImage = this.ParseFromInstruction(construct, escapeChar, stageNameMap);
                        break;
                    case "CopyInstruction":
                        baseImage = this.ParseCopyInstruction(construct, escapeChar, stageNameMap);
                        break;
                    default:
                        break;
                }
            }

            return baseImage;
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to detect a DockerReference component, the component will not be registered.");
            return null;
        }
    }

    private DockerReference ParseFromInstruction(DockerfileConstruct construct, char escapeChar, Dictionary<string, string> stageNameMap)
    {
        var tokens = construct.Tokens.ToArray();
        var resolvedFromStatement = construct.ResolveVariables(escapeChar).TrimEnd();
        var fromInstruction = (FromInstruction)construct;
        var reference = fromInstruction.ImageName;
        if (string.IsNullOrWhiteSpace(resolvedFromStatement) || string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var stageName = fromInstruction.StageName;
        stageNameMap.TryGetValue(reference, out var stageNameReference);

        if (!string.IsNullOrEmpty(stageName))
        {
            if (!string.IsNullOrEmpty(stageNameReference))
            {
                stageNameMap.Add(stageName, stageNameReference);
            }
            else
            {
                stageNameMap.Add(stageName, reference);
            }
        }

        if (!string.IsNullOrEmpty(stageNameReference))
        {
            if (this.HasUnresolvedVariables(stageNameReference))
            {
                return null;
            }

            return DockerReferenceUtility.ParseFamiliarName(stageNameReference);
        }

        if (this.HasUnresolvedVariables(reference))
        {
            return null;
        }

        return DockerReferenceUtility.ParseFamiliarName(reference);
    }

    private DockerReference ParseCopyInstruction(DockerfileConstruct construct, char escapeChar, Dictionary<string, string> stageNameMap)
    {
        var resolvedCopyStatement = construct.ResolveVariables(escapeChar).TrimEnd();
        var copyInstruction = (CopyInstruction)construct;
        var reference = copyInstruction.FromStageName;
        if (string.IsNullOrWhiteSpace(resolvedCopyStatement) || string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        stageNameMap.TryGetValue(reference, out var stageNameReference);
        if (!string.IsNullOrEmpty(stageNameReference))
        {
            if (this.HasUnresolvedVariables(stageNameReference))
            {
                return null;
            }
            else
            {
                return DockerReferenceUtility.ParseFamiliarName(stageNameReference);
            }
        }

        if (this.HasUnresolvedVariables(reference))
        {
            return null;
        }

        return DockerReferenceUtility.ParseFamiliarName(reference);
    }

    private bool HasUnresolvedVariables(string reference)
    {
        return new Regex("[${}]").IsMatch(reference);
    }
}

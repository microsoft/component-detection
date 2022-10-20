using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Valleysoft.DockerfileModel;

namespace Microsoft.ComponentDetection.Detectors.Dockerfile
{
    [Export(typeof(IComponentDetector))]
    public class DockerfileComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
    {
        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        [Import]
        public IEnvironmentVariableService EnvVarService { get; set; }

        public override string Id { get; } = "DockerReference";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.DockerReference) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "dockerfile", "dockerfile.*", "*.dockerfile" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.DockerReference };

        public override int Version => 1;

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;
            var filePath = file.Location;
            try
            {
                this.Logger.LogInfo($"Discovered dockerfile: {file.Location}");

                string contents;
                using (var reader = new StreamReader(file.Stream))
                {
                    contents = await reader.ReadToEndAsync();
                }

                var stageNameMap = new Dictionary<string, string>();
                var dockerFileComponent = this.ParseDockerFile(contents, file.Location, singleFileComponentRecorder, stageNameMap);
            }
            catch (Exception e)
            {
                this.Logger.LogError($"The file doesn't appear to be a Dockerfile: '{file.Location}'");
                this.Logger.LogException(e, false);
            }
        }

        private Task ParseDockerFile(string fileContents, string fileLocation, ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, string> stageNameMap)
        {
            var dockerfileModel = Valleysoft.DockerfileModel.Dockerfile.Parse(fileContents);
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
                this.Logger.LogError($"Failed to detect a DockerReference component, the component will not be registered. \n Error Message: <{e.Message}>");
                this.Logger.LogException(e, isError: true, printException: true);
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
}

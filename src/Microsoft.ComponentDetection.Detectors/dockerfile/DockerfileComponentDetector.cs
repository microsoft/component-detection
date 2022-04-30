using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valleysoft.DockerfileModel;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Detectors.Dockerfile
{
    [Export(typeof(IComponentDetector))]
    public class DockerfileComponentDetector : FileComponentDetector
    {
        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        [Import]
        public IEnvironmentVariableService EnvVarService { get; set; }

        public override string Id { get; } = "dockerfile";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "dockerfile", "dockerfile.*" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Dockerfile };

        public override int Version => 5;

        private HashSet<string> projectRoots = new HashSet<string>();

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;
            var filePath = file.Location;
            Logger.LogInfo($"Discovered dockerfile: {file.Location}");

            string contents;
            using (var reader = new StreamReader(file.Stream))
            {
                contents = await reader.ReadToEndAsync();
            }

            var stageNameMap = new Dictionary<string, string>();
            var dockerFileComponent = ParseDockerFile(contents, file.Location, singleFileComponentRecorder, stageNameMap);
        }

        private Task ParseDockerFile(string fileContents, string fileLocation, ISingleFileComponentRecorder singleFileComponentRecorder, Dictionary<string, string> stageNameMap)
        {
            var dockerfileModel = Valleysoft.DockerfileModel.Dockerfile.Parse(fileContents);
            var instructions = dockerfileModel.Items;
            foreach (var instruction in instructions)
            {
                var imageReference = ProcessDockerfileConstruct(instruction, dockerfileModel.EscapeChar, stageNameMap);
                if (imageReference != null)
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(imageReference.ToTypedDockerImageComponent()));
                }
            }
            
            return Task.CompletedTask;
        }

        private IDockerReference ProcessDockerfileConstruct(DockerfileConstruct construct, char escapeChar, Dictionary<string, string> stageNameMap)
        {
            var instructionKeyword = construct.Type;
            IDockerReference baseImage = null;
            if (instructionKeyword == ConstructType.Instruction)
            {
                var constructType = construct.GetType().ToString();
                switch (constructType)
                {
                    case "Valleysoft.DockerfileModel.FromInstruction":
                        baseImage = ParseFromInstruction(construct, escapeChar, stageNameMap);
                        break;
                    case "Valleysoft.DockerfileModel.CopyInstruction":
                        baseImage = ParseCopyInstruction(construct, escapeChar, stageNameMap);
                        break;
                    default:
                        break;
                }
            }

            return baseImage;
        }

        private IDockerReference ParseFromInstruction(DockerfileConstruct construct, char escapeChar, Dictionary<string, string> stageNameMap)
        {
            var tokens = construct.Tokens.ToArray();
            var resolvedFromStatement = construct.ResolveVariables(escapeChar).TrimEnd();
            var fromInstruction = (Valleysoft.DockerfileModel.FromInstruction)construct;
            string reference = fromInstruction.ImageName;
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
                if (HasUnresolvedVariables(stageNameReference))
                {
                    return null;
                }

                return DockerReferenceUtility.ParseFamiliarName(stageNameReference);
            }

            if (HasUnresolvedVariables(reference))
            {
                return null;
            }

            return DockerReferenceUtility.ParseFamiliarName(reference);
        }

        private IDockerReference ParseCopyInstruction(DockerfileConstruct construct, char escapeChar, Dictionary<string, string> stageNameMap)
        {
            string reference = string.Empty;
            var resolvedFromStatement = construct.ResolveVariables(escapeChar).TrimEnd();
            if (string.IsNullOrWhiteSpace(resolvedFromStatement))
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

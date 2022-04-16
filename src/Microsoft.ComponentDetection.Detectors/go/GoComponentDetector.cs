using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Detectors.Go
{
    [Export(typeof(IComponentDetector))]
    public class GoComponentDetector : FileComponentDetector
    {
        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        [Import]
        public IEnvironmentVariableService EnvVarService { get; set; }

        private static readonly Regex GoSumRegex = new Regex(
            @"(?<name>.*)\s+(?<version>.*?)(/go\.mod)?\s+(?<hash>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        public override string Id { get; } = "Go";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "go.mod", "go.sum" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Go };

        public override int Version => 5;

        private HashSet<string> projectRoots = new HashSet<string>();

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            var projectRootDirectory = Directory.GetParent(file.Location);
            if (projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
            {
                return;
            }

            var wasGoCliScanSuccessful = false;
            try
            {
                if (IsGoCliManuallyEnabled())
                {
                    Logger.LogInfo("Go cli scan was manually enabled");
                    wasGoCliScanSuccessful = await UseGoCliToScan(file.Location, singleFileComponentRecorder);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to detect components using go cli. Location: {file.Location}");
                Logger.LogException(ex, isError: true, printException: true);
            }
            finally
            {
                if (wasGoCliScanSuccessful)
                {
                    projectRoots.Add(projectRootDirectory.FullName);
                }
                else
                {
                    var fileExtension = Path.GetExtension(file.Location).ToLowerInvariant();
                    switch (fileExtension)
                    {
                        case ".mod":
                            {
                                Logger.LogVerbose("Found Go.mod: " + file.Location);
                                ParseGoModFile(singleFileComponentRecorder, file);
                                break;
                            }

                        case ".sum":
                            {
                                Logger.LogVerbose("Found Go.sum: " + file.Location);
                                ParseGoSumFile(singleFileComponentRecorder, file);
                                break;
                            }

                        default:
                            {
                                throw new Exception("Unexpected file type detected in go detector");
                            }
                    }
                }
            }
        }

        private async Task<bool> UseGoCliToScan(string location, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            using var record = new GoGraphTelemetryRecord();
            record.WasGraphSuccessful = false;

            var projectRootDirectory = Directory.GetParent(location);
            record.ProjectRoot = projectRootDirectory.FullName;

            var isGoAvailable = await CommandLineInvocationService.CanCommandBeLocated("go", null, workingDirectory: projectRootDirectory, new[] { "version" });
            record.IsGoAvailable = isGoAvailable;

            if (!isGoAvailable)
            {
                return false;
            }

            var goDependenciesProcess = await CommandLineInvocationService.ExecuteCommand("go", null, workingDirectory: projectRootDirectory, new[] { "list", "-m", "-json", "all" });
            if (goDependenciesProcess.ExitCode != 0)
            {
                Logger.LogError($"Go CLI could not get dependency build list at location: {location}. Fallback go.sum/go.mod parsing will be used.");
                return false;
            }

            RecordBuildDependencies(goDependenciesProcess.StdOut, singleFileComponentRecorder);

            var generateGraphProcess = await CommandLineInvocationService.ExecuteCommand("go", null, workingDirectory: projectRootDirectory, new List<string> { "mod", "graph" }.ToArray());
            if (generateGraphProcess.ExitCode == 0)
            {
                PopulateDependencyGraph(generateGraphProcess.StdOut, singleFileComponentRecorder);
                record.WasGraphSuccessful = true;
            }

            return true;
        }

        private void ParseGoModFile(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            IComponentStream file)
        {
            using var reader = new StreamReader(file.Stream);

            string line = reader.ReadLine();
            while (line != null && !line.StartsWith("require ("))
            {
                line = reader.ReadLine();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = reader.ReadLine()) != null && !line.EndsWith(")"))
            {
                if (TryToCreateGoComponentFromModLine(line, out var goComponent))
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
                }
                else
                {
                    Logger.LogWarning($"Line could not be parsed for component [{line.Trim()}]");
                }
            }
        }

        private bool TryToCreateGoComponentFromModLine(string line, out GoComponent goComponent)
        {
            var lineComponents = Regex.Split(line.Trim(), @"\s+");

            if (lineComponents.Length < 2)
            {
                goComponent = null;
                return false;
            }

            var name = lineComponents[0];
            var version = lineComponents[1];
            goComponent = new GoComponent(name, version);

            return true;
        }

        //For more information about the format of the go.sum file
        //visit https://golang.org/cmd/go/#hdr-Module_authentication_using_go_sum
        private void ParseGoSumFile(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            IComponentStream file)
        {
            using var reader = new StreamReader(file.Stream);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (TryToCreateGoComponentFromSumLine(line, out var goComponent))
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
                }
                else
                {
                    Logger.LogWarning($"Line could not be parsed for component [{line.Trim()}]");
                }
            }
        }

        private bool TryToCreateGoComponentFromSumLine(string line, out GoComponent goComponent)
        {
            Match m = GoSumRegex.Match(line);
            if (m.Success)
            {
                goComponent = new GoComponent(m.Groups["name"].Value, m.Groups["version"].Value, m.Groups["hash"].Value);
                return true;
            }

            goComponent = null;
            return false;
        }

        /// <summary>
        /// This command only adds edges between parent and child components, it does not add nor remove any entries from the existing build list.
        /// </summary>
        private void PopulateDependencyGraph(string goGraphOutput, ISingleFileComponentRecorder componentRecorder)
        {
            // Yes, go always returns \n even on Windows
            var graphRelationships = goGraphOutput.Split('\n');

            foreach (var relationship in graphRelationships)
            {
                var components = relationship.Split(' ');
                if (components.Length != 2)
                {
                    Logger.LogWarning("Unexpected relationship output from go mod graph:");
                    Logger.LogWarning(relationship);
                    continue;
                }

                GoComponent parentComponent;
                GoComponent childComponent;

                var isParentParsed = TryCreateGoComponentFromRelationshipPart(components[0], out parentComponent);
                var isChildParsed = TryCreateGoComponentFromRelationshipPart(components[1], out childComponent);

                if (!isParentParsed)
                {
                    // These are explicit dependencies, we already have those recorded
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
                    Logger.LogWarning($"Failed to parse components from relationship string {relationship}");
                }
            }
        }

        private bool IsModuleInBuildList(ISingleFileComponentRecorder singleFileComponentRecorder, GoComponent component)
        {
            return singleFileComponentRecorder.GetComponent(component.Id) != null;
        }

        private void RecordBuildDependencies(string goListOutput, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            // Yes, go always returns \n even on Windows
            var goListObjects = goListOutput.Split("}\n{").Where(x => !string.IsNullOrWhiteSpace(x));

            // The json output has multiple roots. We need to update it to make it a valid string
            // Namely do the following conversion { "propA": 1}{"propA": 2} => [{ "propA": 1},{"propA": 2}]
            // Yes, go always returns \n even on Windows
            var validJsonList = $"[{string.Join("},{", goListObjects)}]";
            var goBuildModules = JsonConvert.DeserializeObject<List<GoBuildModule>>(validJsonList);

            foreach (var dependency in goBuildModules)
            {
                if (dependency.Main)
                {
                    // main is the entry point module (superfluous as we already have the file location)
                    continue;
                }

                var goComponent = new GoComponent(dependency.Path, dependency.Version);

                if (dependency.Indirect)
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
                }
                else
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent), isExplicitReferencedDependency: true);
                }
            }
        }

        private bool TryCreateGoComponentFromRelationshipPart(string relationship, out GoComponent goComponent)
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

        private bool IsGoCliManuallyEnabled()
        {
            return EnvVarService.DoesEnvironmentVariableExist("EnableGoCliScan");
        }

        private class GoBuildModule
        {
            public string Path { get; set; }

            public bool Main { get; set; }

            public string Version { get; set; }

            public bool Indirect { get; set; }
        }
    }
}

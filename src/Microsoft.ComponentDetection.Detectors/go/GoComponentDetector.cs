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
        private static readonly Regex GoSumRegex = new Regex(
            @"(?<name>.*)\s+(?<version>.*?)(/go\.mod)?\s+(?<hash>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        private readonly HashSet<string> projectRoots = new HashSet<string>();

        [Import]
        public ICommandLineInvocationService CommandLineInvocationService { get; set; }

        [Import]
        public IEnvironmentVariableService EnvVarService { get; set; }

        public override string Id { get; } = "Go";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "go.mod", "go.sum" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Go };

        public override int Version => 6;

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            var projectRootDirectory = Directory.GetParent(file.Location);
            if (this.projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
            {
                return;
            }

            var wasGoCliScanSuccessful = false;
            try
            {
                if (!this.IsGoCliManuallyDisabled())
                {
                    wasGoCliScanSuccessful = await this.UseGoCliToScan(file.Location, singleFileComponentRecorder);
                }
                else
                {
                    this.Logger.LogInfo("Go cli scan was manually disabled, fallback strategy performed." +
                                        " More info: https://github.com/microsoft/component-detection/blob/main/docs/detectors/go.md#fallback-detection-strategy");
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError($"Failed to detect components using go cli. Location: {file.Location}");
                this.Logger.LogException(ex, isError: true, printException: true);
            }
            finally
            {
                if (wasGoCliScanSuccessful)
                {
                    this.projectRoots.Add(projectRootDirectory.FullName);
                }
                else
                {
                    var fileExtension = Path.GetExtension(file.Location).ToLowerInvariant();
                    switch (fileExtension)
                    {
                        case ".mod":
                            {
                                this.Logger.LogVerbose("Found Go.mod: " + file.Location);
                                this.ParseGoModFile(singleFileComponentRecorder, file);
                                break;
                            }

                        case ".sum":
                            {
                                this.Logger.LogVerbose("Found Go.sum: " + file.Location);
                                this.ParseGoSumFile(singleFileComponentRecorder, file);
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

            var isGoAvailable = await this.CommandLineInvocationService.CanCommandBeLocated("go", null, workingDirectory: projectRootDirectory, new[] { "version" });
            record.IsGoAvailable = isGoAvailable;

            if (!isGoAvailable)
            {
                this.Logger.LogInfo("Go CLI was not found in the system");
                return false;
            }

            this.Logger.LogInfo("Go CLI was found in system and will be used to generate dependency graph. " +
                                "Detection time may be improved by activating fallback strategy (https://github.com/microsoft/component-detection/blob/main/docs/detectors/go.md#fallback-detection-strategy). " +
                                "But, it will introduce noise into the detected components.");
            var goDependenciesProcess = await this.CommandLineInvocationService.ExecuteCommand("go", null, workingDirectory: projectRootDirectory, new[] { "list", "-mod=readonly", "-m", "-json", "all" });
            if (goDependenciesProcess.ExitCode != 0)
            {
                this.Logger.LogError($"Go CLI command \"go list -m -json all\" failed with error:\n {goDependenciesProcess.StdErr}");
                this.Logger.LogError($"Go CLI could not get dependency build list at location: {location}. Fallback go.sum/go.mod parsing will be used.");
                return false;
            }

            this.RecordBuildDependencies(goDependenciesProcess.StdOut, singleFileComponentRecorder);

            var generateGraphProcess = await this.CommandLineInvocationService.ExecuteCommand("go", null, workingDirectory: projectRootDirectory, new List<string> { "mod", "graph" }.ToArray());
            if (generateGraphProcess.ExitCode == 0)
            {
                this.PopulateDependencyGraph(generateGraphProcess.StdOut, singleFileComponentRecorder);
                record.WasGraphSuccessful = true;
            }

            return true;
        }

        private void ParseGoModFile(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            IComponentStream file)
        {
            using var reader = new StreamReader(file.Stream);

            var line = reader.ReadLine();
            while (line != null && !line.StartsWith("require ("))
            {
                line = reader.ReadLine();
            }

            // Stopping at the first ) restrict the detection to only the require section.
            while ((line = reader.ReadLine()) != null && !line.EndsWith(")"))
            {
                if (this.TryToCreateGoComponentFromModLine(line, out var goComponent))
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
                }
                else
                {
                    this.Logger.LogWarning($"Line could not be parsed for component [{line.Trim()}]");
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

        // For more information about the format of the go.sum file
        // visit https://golang.org/cmd/go/#hdr-Module_authentication_using_go_sum
        private void ParseGoSumFile(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            IComponentStream file)
        {
            using var reader = new StreamReader(file.Stream);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (this.TryToCreateGoComponentFromSumLine(line, out var goComponent))
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
                }
                else
                {
                    this.Logger.LogWarning($"Line could not be parsed for component [{line.Trim()}]");
                }
            }
        }

        private bool TryToCreateGoComponentFromSumLine(string line, out GoComponent goComponent)
        {
            var m = GoSumRegex.Match(line);
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
                    if (string.IsNullOrWhiteSpace(relationship))
                    {
                        // normally the last line is an empty string
                        continue;
                    }

                    this.Logger.LogWarning("Unexpected relationship output from go mod graph:");
                    this.Logger.LogWarning(relationship);
                    continue;
                }

                var isParentParsed = this.TryCreateGoComponentFromRelationshipPart(components[0], out var parentComponent);
                var isChildParsed = this.TryCreateGoComponentFromRelationshipPart(components[1], out var childComponent);

                if (!isParentParsed)
                {
                    // These are explicit dependencies, we already have those recorded
                    continue;
                }

                if (isChildParsed)
                {
                    if (this.IsModuleInBuildList(componentRecorder, parentComponent) && this.IsModuleInBuildList(componentRecorder, childComponent))
                    {
                        componentRecorder.RegisterUsage(new DetectedComponent(childComponent), parentComponentId: parentComponent.Id);
                    }
                }
                else
                {
                    this.Logger.LogWarning($"Failed to parse components from relationship string {relationship}");
                }
            }
        }

        private bool IsModuleInBuildList(ISingleFileComponentRecorder singleFileComponentRecorder, GoComponent component)
        {
            return singleFileComponentRecorder.GetComponent(component.Id) != null;
        }

        private void RecordBuildDependencies(string goListOutput, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            var goBuildModules = new List<GoBuildModule>();
            var reader = new JsonTextReader(new StringReader(goListOutput))
            {
                SupportMultipleContent = true,
            };

            while (reader.Read())
            {
                var serializer = new JsonSerializer();
                var buildModule = serializer.Deserialize<GoBuildModule>(reader);

                goBuildModules.Add(buildModule);
            }

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

        private bool IsGoCliManuallyDisabled()
        {
            return this.EnvVarService.IsEnvironmentVariableValueTrue("DisableGoCliScan");
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

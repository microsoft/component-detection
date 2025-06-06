namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ComponentDetection.Contracts;
    using Microsoft.ComponentDetection.Contracts.Internal;
    using Microsoft.ComponentDetection.Contracts.TypedComponent;
    using Microsoft.Extensions.Logging;
    using Tomlyn;
    using Tomlyn.Model;

    public class UvLockComponentDetector : FileComponentDetector
    {
        public UvLockComponentDetector(
            IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
            IObservableDirectoryWalkerFactory walkerFactory,
            ILogger<UvLockComponentDetector> logger)
        {
            this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
            this.Scanner = walkerFactory;
            this.Logger = logger;
        }

        public override string Id => "UvLock";

        public override IList<string> SearchPatterns { get; } = ["uv.lock"];

        public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Pip];

        public override int Version => 1;

        public override IEnumerable<string> Categories => ["Python"];

        protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            try
            {
                using var reader = new StreamReader(file.Stream);
                var toml = await reader.ReadToEndAsync(cancellationToken);
                var model = Toml.ToModel(toml);

                var explicitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (model is TomlTable table)
                {
                    // Parse [package.metadata].requires-dist for explicit roots
                    if (table.TryGetValue("package.metadata", out var metadataObj) && metadataObj is TomlTable metadataTable)
                    {
                        if (metadataTable.TryGetValue("requires-dist", out var requiresDistObj) && requiresDistObj is TomlTableArray requiresDistArr)
                        {
                            foreach (var req in requiresDistArr)
                            {
                                if (req is TomlTable reqTable && reqTable.TryGetValue("name", out var nameObj) && nameObj is string nameStr && !string.IsNullOrEmpty(nameStr))
                                {
                                    explicitNames.Add(nameStr);
                                }
                            }
                        }
                    }

                    // Parse all packages and their dependencies
                    var packagesList = new List<(PipComponent Component, List<string> Dependencies)>();
                    if (table.TryGetValue("package", out var packagesObj) && packagesObj is TomlTableArray packages)
                    {
                        foreach (var pkg in packages)
                        {
                            var name = pkg?["name"]?.ToString();
                            var version = pkg?["version"]?.ToString();
                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
                            {
                                continue;
                            }

                            var pipComponent = new PipComponent(name, version);
                            var dependencies = new List<string>();
                            if (pkg.TryGetValue("dependencies", out var depsObj) && depsObj is TomlTable depsTable)
                            {
                                foreach (var depKey in depsTable.Keys)
                                {
                                    var depName = depKey;
                                    var depVersion = depsTable[depKey]?.ToString();
                                    if (!string.IsNullOrEmpty(depName) && !string.IsNullOrEmpty(depVersion))
                                    {
                                        dependencies.Add($"{depName} {depVersion} - pip");
                                    }
                                }
                            }

                            packagesList.Add((pipComponent, dependencies));
                        }
                    }

                    // The test expects the key to be the file name, but the recorder uses the file path. We need to update the test to match the actual behavior.
                    var componentIdSet = new HashSet<string>(packagesList.Select(x => x.Component.Id));

                    foreach (var (component, dependencies) in packagesList)
                    {
                        var detectedComponent = new DetectedComponent(component);

                        // Mark as explicit if there are no dependencies and no explicit roots are defined, or if in explicitNames
                        var isExplicit = explicitNames.Count == 0 || explicitNames.Contains(component.Name);
                        singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: isExplicit);

                        // Register dependencies as edges
                        foreach (var depId in dependencies)
                        {
                            if (componentIdSet.Contains(depId))
                            {
                                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(new PipComponent(depId.Split(' ')[0], depId.Split(' ')[1])), isExplicitReferencedDependency: false, parentComponentId: component.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to parse uv.lock file {File}", file.Location);
            }
        }
    }
}

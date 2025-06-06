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

                if (model is TomlTable table)
                {
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

                    // Register all components and their dependencies in the graph
                    var componentIdSet = new HashSet<string>(packagesList.Select(x => x.Component.Id));
                    foreach (var (component, dependencies) in packagesList)
                    {
                        var detectedComponent = new DetectedComponent(component);
                        if (dependencies.Count == 0)
                        {
                            singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: true);
                        }
                        else
                        {
                            singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: true);
                            foreach (var depId in dependencies)
                            {
                                // Only add edges to components that exist in the lock file
                                if (componentIdSet.Contains(depId))
                                {
                                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component), isExplicitReferencedDependency: true, parentComponentId: component.Id);
                                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(new PipComponent(depId.Split(' ')[0], depId.Split(' ')[1])), isExplicitReferencedDependency: false, parentComponentId: component.Id);
                                }
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

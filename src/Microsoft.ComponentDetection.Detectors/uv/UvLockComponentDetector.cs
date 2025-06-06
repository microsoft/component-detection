namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ComponentDetection.Contracts;
    using Microsoft.ComponentDetection.Contracts.Internal;
    using Microsoft.ComponentDetection.Contracts.TypedComponent;
    using Microsoft.Extensions.Logging;

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

        protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            try
            {
                // Parse the file stream into a UvLock model
                file.Stream.Position = 0; // Ensure stream is at the beginning
                var uvLock = UvLock.Parse(file.Stream);

                // Determine explicit roots from the TOML (optional, not implemented in UvLock yet)
                // For now, mark all as explicit if no explicit roots are defined
                var explicitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Register all packages and their dependencies
                var componentIdSet = new HashSet<string>(uvLock.Packages.Select(x => new PipComponent(x.Name, x.Version).Id));
                foreach (var pkg in uvLock.Packages)
                {
                    var pipComponent = new PipComponent(pkg.Name, pkg.Version);
                    var detectedComponent = new DetectedComponent(pipComponent);
                    var isExplicit = explicitNames.Count == 0 || explicitNames.Contains(pkg.Name);
                    singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: isExplicit);

                    foreach (var dep in pkg.Dependencies)
                    {
                        // Only register edge if dependency is in the lock file
                        var depPkg = uvLock.Packages.FirstOrDefault(p => p.Name.Equals(dep.Name, StringComparison.OrdinalIgnoreCase));
                        if (depPkg != null)
                        {
                            var depComponentWithVersion = new PipComponent(depPkg.Name, depPkg.Version);
                            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(depComponentWithVersion), isExplicitReferencedDependency: false, parentComponentId: pipComponent.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to parse uv.lock file {File}", file.Location);
            }

            return Task.CompletedTask;
        }
    }
}

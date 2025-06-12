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

    public class UvLockComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
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

        internal static bool IsRootPackage(UvPackage pck)
        {
            return pck.Source?.Virtual != null;
        }

        protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            try
            {
                // Parse the file stream into a UvLock model
                file.Stream.Position = 0; // Ensure stream is at the beginning
                var uvLock = UvLock.Parse(file.Stream);

                var rootPackage = uvLock.Packages.FirstOrDefault(IsRootPackage);

                // Add requires-dist as explicitly referenced component ids and dependencies
                foreach (var dep in rootPackage.MetadataRequiresDist)
                {
                    var depComponent = new PipComponent(dep.Name, dep.Specifier);
                    var detectedDep = new DetectedComponent(depComponent);
                    singleFileComponentRecorder.RegisterUsage(detectedDep, isExplicitReferencedDependency: true, isDevelopmentDependency: false);
                }

                var devPackages = new HashSet<string>();
                foreach (var devDep in rootPackage.MetadataRequiresDev)
                {
                    devPackages.Add(devDep.Name);
                }

                foreach (var pkg in uvLock.Packages)
                {
                    if (IsRootPackage(pkg))
                    {
                        continue;
                    }

                    var pipComponent = new PipComponent(pkg.Name, pkg.Version);
                    var isDevelopmentDependency = devPackages.Contains(pkg.Name);
                    var detectedComponent = new DetectedComponent(pipComponent);
                    singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: isDevelopmentDependency);

                    foreach (var dep in pkg.Dependencies)
                    {
                        var depPkg = uvLock.Packages.FirstOrDefault(p => p.Name.Equals(dep.Name, StringComparison.OrdinalIgnoreCase));
                        if (depPkg != null)
                        {
                            var depComponentWithVersion = new PipComponent(depPkg.Name, depPkg.Version);
                            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(depComponentWithVersion), isExplicitReferencedDependency: false, parentComponentId: pipComponent.Id);
                        }
                        else
                        {
                            this.Logger.LogWarning("Dependency {DependencyName} not found in uv.lock packages", dep.Name);
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

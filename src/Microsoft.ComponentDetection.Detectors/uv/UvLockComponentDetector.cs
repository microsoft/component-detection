namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Uv];

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
                    // Optionally, log or use top-level metadata
                    var topLevelMetadata = new Dictionary<string, object>();
                    foreach (var key in table.Keys)
                    {
                        if (key != "package")
                        {
                            topLevelMetadata[key] = table[key];
                        }
                    }

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

                            var uvComponent = new UvComponent(name, version);
                            var detectedComponent = new DetectedComponent(uvComponent);
                            singleFileComponentRecorder.RegisterUsage(detectedComponent);
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

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry.Contracts;
using Nett;

namespace Microsoft.ComponentDetection.Detectors.Poetry
{
    [Export(typeof(IComponentDetector))]
    public class PoetryComponentDetector : FileComponentDetector
    {
        public override string Id => "Poetry";

        public override IList<string> SearchPatterns { get; } = new List<string> { "poetry.lock" };

        public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Pip };

        public override int Version { get; } = 2;

        public override IEnumerable<string> Categories => new List<string> { "Python" };

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var poetryLockFile = processRequest.ComponentStream;
            Logger.LogVerbose("Found Poetry lockfile: " + poetryLockFile);

            var poetryLock = StreamTomlSerializer.Deserialize(poetryLockFile.Stream, TomlSettings.Create()).Get<PoetryLock>();
            poetryLock.package.ToList().ForEach(package =>
            {
                var isDevelopmentDependency = package.category != "main";

                if (package.source != null && package.source.type == "git")
                {
                    var component = new DetectedComponent(new GitComponent(new Uri(package.source.url), package.source.resolved_reference));
                    singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: isDevelopmentDependency);
                }
                else
                {
                    var component = new DetectedComponent(new PipComponent(package.name, package.version));
                    singleFileComponentRecorder.RegisterUsage(component, isDevelopmentDependency: isDevelopmentDependency);
                }
            });

            return Task.CompletedTask;
        }
    }
}

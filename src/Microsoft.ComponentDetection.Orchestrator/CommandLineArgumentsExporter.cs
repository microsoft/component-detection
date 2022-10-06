using System;
using System.Composition;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

namespace Microsoft.ComponentDetection.Orchestrator
{
    [Export]
    public class CommandLineArgumentsExporter
    {
        public static IScanArguments ArgumentsForDelayedInjection { get; set; }

        public CommandLineArgumentsExporter()
        {
            this.DelayedInjectionLazy = new Lazy<IScanArguments>(() => ArgumentsForDelayedInjection);
        }

        [Export("InjectableDetectionArguments")]
        public Lazy<IScanArguments> DelayedInjectionLazy { get; set; }
    }
}

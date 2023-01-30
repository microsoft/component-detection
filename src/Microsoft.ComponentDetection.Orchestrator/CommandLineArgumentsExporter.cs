namespace Microsoft.ComponentDetection.Orchestrator;
using System;
using System.Composition;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

[Export]
public class CommandLineArgumentsExporter
{
    public CommandLineArgumentsExporter() => this.DelayedInjectionLazy = new Lazy<IScanArguments>(() => ArgumentsForDelayedInjection);

    public static IScanArguments ArgumentsForDelayedInjection { get; set; }

    [Export("InjectableDetectionArguments")]
    public Lazy<IScanArguments> DelayedInjectionLazy { get; set; }
}

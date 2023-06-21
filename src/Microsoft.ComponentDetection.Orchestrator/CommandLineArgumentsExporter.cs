namespace Microsoft.ComponentDetection.Orchestrator;

using System;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;

public class CommandLineArgumentsExporter
{
    public CommandLineArgumentsExporter() => this.DelayedInjectionLazy = new Lazy<IScanArguments>(() => ArgumentsForDelayedInjection);

    public static IScanArguments ArgumentsForDelayedInjection { get; set; }

    public Lazy<IScanArguments> DelayedInjectionLazy { get; set; }
}

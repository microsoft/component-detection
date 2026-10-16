namespace Microsoft.ComponentDetection.Detectors.Experiments;

using System;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Records discoveries from a detector as additions against an empty control group.
/// </summary>
/// <typeparam name="TDetector">The experimental detector.</typeparam>
public class EmptyBaselineExperiment<TDetector> : IExperimentConfiguration
    where TDetector : IExperimentalDetector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyBaselineExperiment{TDetector}"/> class.
    /// </summary>
    /// <param name="name">The name of the experiment.</param>
    public EmptyBaselineExperiment(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        this.Name = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Task InitAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public bool IsInControlGroup(IComponentDetector componentDetector) => false;

    /// <inheritdoc />
    public bool IsInExperimentGroup(IComponentDetector componentDetector) => componentDetector is TDetector;

    /// <inheritdoc />
    public bool ShouldRecord(IComponentDetector componentDetector, int numComponents) => true;
}

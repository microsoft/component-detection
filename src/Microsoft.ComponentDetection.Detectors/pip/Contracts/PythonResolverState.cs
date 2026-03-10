#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;

public class PythonResolverState
{
    public IDictionary<string, SortedDictionary<string, IList<PythonProjectRelease>>> ValidVersionMap { get; }
        = new Dictionary<string, SortedDictionary<string, IList<PythonProjectRelease>>>(StringComparer.OrdinalIgnoreCase);

    public Queue<(string PackageName, PipDependencySpecification Package)> ProcessingQueue { get; } = new Queue<(string, PipDependencySpecification)>();

    public IDictionary<string, PipGraphNode> NodeReferences { get; } = new Dictionary<string, PipGraphNode>(StringComparer.OrdinalIgnoreCase);

    public IList<PipGraphNode> Roots { get; } = [];
}

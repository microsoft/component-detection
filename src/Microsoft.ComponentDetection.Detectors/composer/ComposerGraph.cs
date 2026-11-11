namespace Microsoft.ComponentDetection.Detectors.Composer;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.ComponentDetection.Contracts;

/// <summary>Infers candidate roots from source strongly connected groups when no root manifest exists.</summary>
internal static class ComposerGraph
{
    internal static HashSet<string> FindRoots(IDependencyGraph graph, IEnumerable<string> componentIds, CancellationToken cancellationToken)
    {
        // Iterative Kosaraju traversal avoids exhausting the stack on long dependency chains.
        var visited = new HashSet<string>();
        var order = new List<string>();
        foreach (var start in componentIds)
        {
            var stack = new Stack<(string Id, bool Finished)>();
            stack.Push((start, false));
            while (stack.TryPop(out var entry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Finished)
                {
                    order.Add(entry.Id);
                }
                else if (visited.Add(entry.Id))
                {
                    stack.Push((entry.Id, true));
                    foreach (var child in graph.GetDependenciesForComponent(entry.Id))
                    {
                        if (!visited.Contains(child))
                        {
                            stack.Push((child, false));
                        }
                    }
                }
            }
        }

        var reverse = componentIds.ToDictionary(id => id, _ => new HashSet<string>());
        foreach (var parent in componentIds)
        {
            foreach (var child in graph.GetDependenciesForComponent(parent))
            {
                reverse[child].Add(parent);
            }
        }

        var groups = new Dictionary<string, int>();
        for (var index = order.Count - 1; index >= 0; index--)
        {
            var stack = new Stack<string>();
            stack.Push(order[index]);
            while (stack.TryPop(out var id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (groups.TryAdd(id, index))
                {
                    foreach (var parent in reverse[id])
                    {
                        stack.Push(parent);
                    }
                }
            }
        }

        var nonRootGroups = new HashSet<int>();
        foreach (var (child, parents) in reverse)
        {
            foreach (var parent in parents)
            {
                if (groups[parent] != groups[child])
                {
                    nonRootGroups.Add(groups[child]);
                }
            }
        }

        return groups.Where(pair => !nonRootGroups.Contains(pair.Value)).Select(pair => pair.Key).ToHashSet();
    }
}

using System.Collections.ObjectModel;

namespace Microsoft.ComponentDetection.Detectors.Maven
{
    /// <summary>
    /// Internal state holder used by Maven detector.
    /// </summary>
    /// <typeparam name="T">Node type.</typeparam>
    public class GraphNode<T>
    {
        public GraphNode(T value)
        {
            this.Value = value;
        }

        public T Value { get; set; }

        public Collection<GraphNode<T>> Children { get; } = new Collection<GraphNode<T>>();

        public Collection<GraphNode<T>> Parents { get; } = new Collection<GraphNode<T>>();
    }
}

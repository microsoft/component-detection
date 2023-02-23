﻿namespace Microsoft.ComponentDetection.Detectors.Maven;
using System.Collections.Generic;

/// <summary>
/// Internal state holder used by Maven detector.
/// </summary>
/// <typeparam name="T">Node type.</typeparam>
public class GraphNode<T>
{
    public GraphNode(T value) => this.Value = value;

    public T Value { get; set; }

    public List<GraphNode<T>> Children { get; } = new List<GraphNode<T>>();

    public List<GraphNode<T>> Parents { get; } = new List<GraphNode<T>>();
}

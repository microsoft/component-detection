using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Detectors.Maven
{
    public class MavenStyleDependencyGraphParser
    {
        public GraphNode<string> DependencyCategory { get; private set; }

        private Stack<GraphNodeAtLevel<string>> stack = new Stack<GraphNodeAtLevel<string>>();

        private Stack<(int ParseLevel, DetectedComponent Component)> tupleStack = new Stack<(int, DetectedComponent)>();

        private DetectedComponent topLevelComponent = null;

        private static readonly char[] TrimCharacters = new char[] { '|', ' ' };

        private static readonly string[] ComponentSplitters = new[] { "+-", "\\-" };

        private void StartDependencyCategory(string categoryName)
        {
            if (DependencyCategory != null)
            {
                throw new InvalidOperationException("Current category must be finished before starting new category.");
            }

            DependencyCategory = new GraphNode<string>(categoryName);
        }

        public GraphNode<string> Parse(string[] lines)
        {
            foreach (var line in lines)
            {
                var localLine = line.Trim(TrimCharacters);
                if (!string.IsNullOrWhiteSpace(localLine) && DependencyCategory == null)
                {
                    StartDependencyCategory(localLine);
                }
                else
                {
                    var splitterOrDefault = ComponentSplitters.FirstOrDefault(x => localLine.Contains(x));
                    if (splitterOrDefault != null)
                    {
                        TrackDependency(line.IndexOf(splitterOrDefault), localLine.Split(new[] { splitterOrDefault }, StringSplitOptions.None)[1].Trim());
                    }
                }
            }

            return DependencyCategory;
        }

        public void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            foreach (var line in lines)
            {
                var localLine = line.Trim(TrimCharacters);
                if (!string.IsNullOrWhiteSpace(localLine) && topLevelComponent == null)
                {
                    var topLevelMavenStringInfo = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString(localLine);
                    topLevelComponent = topLevelMavenStringInfo.Component;
                    singleFileComponentRecorder.RegisterUsage(
                        topLevelMavenStringInfo.Component, 
                        isDevelopmentDependency: topLevelMavenStringInfo.IsDevelopmentDependency, 
                        dependencyScope: topLevelMavenStringInfo.dependencyScope);
                }
                else
                {
                    var splitterOrDefault = ComponentSplitters.FirstOrDefault(x => localLine.Contains(x));

                    if (splitterOrDefault != null)
                    {
                        RecordDependencies(line.IndexOf(splitterOrDefault), localLine.Split(new[] { splitterOrDefault }, StringSplitOptions.None)[1].Trim(), singleFileComponentRecorder);
                    }
                }
            }
        }

        private void TrackDependency(int position, string versionedComponent)
        {
            while (stack.Count > 0 && stack.Peek().ParseLevel >= position)
            {
                stack.Pop();
            }

            var myNode = new GraphNodeAtLevel<string>(position, versionedComponent);

            if (stack.Count > 0)
            {
                var parent = stack.Peek();
                parent.Children.Add(myNode);
                myNode.Parents.Add(parent);
            }
            else
            {
                DependencyCategory.Children.Add(myNode);
            }

            stack.Push(myNode);
        }

        private void RecordDependencies(int position, string versionedComponent, ISingleFileComponentRecorder componentRecorder)
        {
            while (tupleStack.Count > 0 && tupleStack.Peek().ParseLevel >= position)
            {
                tupleStack.Pop();
            }

            var componentAndDevDependencyTuple = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString(versionedComponent);
            var newTuple = (ParseLevel: position, componentAndDevDependencyTuple.Component);

            if (tupleStack.Count > 0)
            {
                var parent = tupleStack.Peek().Component;
                componentRecorder.RegisterUsage(parent);
                componentRecorder.RegisterUsage(
                    newTuple.Component,
                    parentComponentId: parent.Component.Id,
                    isDevelopmentDependency: componentAndDevDependencyTuple.IsDevelopmentDependency,
                    dependencyScope: componentAndDevDependencyTuple.dependencyScope);
            }
            else
            {
                componentRecorder.RegisterUsage(
                    newTuple.Component,
                    isExplicitReferencedDependency: true,
                    parentComponentId: topLevelComponent.Component.Id,
                    isDevelopmentDependency: componentAndDevDependencyTuple.IsDevelopmentDependency,
                    dependencyScope: componentAndDevDependencyTuple.dependencyScope);
            }

            tupleStack.Push(newTuple);
        }

        private class GraphNodeAtLevel<T> : GraphNode<T>
        {
            public int ParseLevel { get; }

            public GraphNodeAtLevel(int level, T value)
                : base(value)
            {
                ParseLevel = level;
            }
        }
    }
}

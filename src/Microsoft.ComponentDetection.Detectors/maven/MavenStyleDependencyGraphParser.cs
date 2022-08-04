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
            if (this.DependencyCategory != null)
            {
                throw new InvalidOperationException("Current category must be finished before starting new category.");
            }

            this.DependencyCategory = new GraphNode<string>(categoryName);
        }

        public GraphNode<string> Parse(string[] lines)
        {
            foreach (var line in lines)
            {
                var localLine = line.Trim(TrimCharacters);
                if (!string.IsNullOrWhiteSpace(localLine) && this.DependencyCategory == null)
                {
                    this.StartDependencyCategory(localLine);
                }
                else
                {
                    var splitterOrDefault = ComponentSplitters.FirstOrDefault(x => localLine.Contains(x));
                    if (splitterOrDefault != null)
                    {
                        this.TrackDependency(line.IndexOf(splitterOrDefault), localLine.Split(new[] { splitterOrDefault }, StringSplitOptions.None)[1].Trim());
                    }
                }
            }

            return this.DependencyCategory;
        }

        public void Parse(string[] lines, ISingleFileComponentRecorder singleFileComponentRecorder)
        {
            foreach (var line in lines)
            {
                var localLine = line.Trim(TrimCharacters);
                if (!string.IsNullOrWhiteSpace(localLine) && this.topLevelComponent == null)
                {
                    var topLevelMavenStringInfo = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString(localLine);
                    this.topLevelComponent = topLevelMavenStringInfo.Component;
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
                        this.RecordDependencies(line.IndexOf(splitterOrDefault), localLine.Split(new[] { splitterOrDefault }, StringSplitOptions.None)[1].Trim(), singleFileComponentRecorder);
                    }
                }
            }
        }

        private void TrackDependency(int position, string versionedComponent)
        {
            while (this.stack.Count > 0 && this.stack.Peek().ParseLevel >= position)
            {
                this.stack.Pop();
            }

            var myNode = new GraphNodeAtLevel<string>(position, versionedComponent);

            if (this.stack.Count > 0)
            {
                var parent = this.stack.Peek();
                parent.Children.Add(myNode);
                myNode.Parents.Add(parent);
            }
            else
            {
                this.DependencyCategory.Children.Add(myNode);
            }

            this.stack.Push(myNode);
        }

        private void RecordDependencies(int position, string versionedComponent, ISingleFileComponentRecorder componentRecorder)
        {
            while (this.tupleStack.Count > 0 && this.tupleStack.Peek().ParseLevel >= position)
            {
                this.tupleStack.Pop();
            }

            var componentAndDevDependencyTuple = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString(versionedComponent);
            var newTuple = (ParseLevel: position, componentAndDevDependencyTuple.Component);

            if (this.tupleStack.Count > 0)
            {
                var parent = this.tupleStack.Peek().Component;
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
                    parentComponentId: this.topLevelComponent.Component.Id,
                    isDevelopmentDependency: componentAndDevDependencyTuple.IsDevelopmentDependency,
                    dependencyScope: componentAndDevDependencyTuple.dependencyScope);
            }

            this.tupleStack.Push(newTuple);
        }

        private class GraphNodeAtLevel<T> : GraphNode<T>
        {
            public int ParseLevel { get; }

            public GraphNodeAtLevel(int level, T value)
                : base(value)
            {
                this.ParseLevel = level;
            }
        }
    }
}

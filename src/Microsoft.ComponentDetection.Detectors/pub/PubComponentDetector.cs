

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.ComponentDetection.Detectors.Pub
{
    

    [Export(typeof(IComponentDetector))]
    public class PubComponentDetector : FileComponentDetector
    {

        public override string Id { get; } = "Pub";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.RubyGems) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "Pubspec.yaml" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Pub };

        public override int Version { get; } = 1;

        private enum SectionType
        {
            GEM,
            GIT,
            PATH,
        }

        private class Dependency
        {
            public string Name { get; }

            public string Location { get; }

            public string Id => $"{Name}:{Location}";

            public Dependency(string name, string location)
            {
                Name = name;
                Location = location;
            }
        }

        private class Pubspec {
            public string name;
            public string description;
            public string version;
            public string homepage;
            public string documentation; 
            public Dictionary<string, string> environment; 
            public Dictionary<string, string> dependencies;
            public Dictionary<string, string> dev_dependencies;

        }

        public PubComponentDetector()
        {
            NeedsAutomaticRootDependencyCalculation = true;
        }

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            Logger.LogVerbose("Found Pubspec.yaml: " + file.Location);
            ParsePubspecYamlFile(singleFileComponentRecorder, file);

            return Task.CompletedTask;
        }

        private void ParsePubspecYamlFile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream file)
        {
            var components = new Dictionary<string, DetectedComponent>();
            var dependencies = new Dictionary<string, List<Dependency>>();

            var text = string.Empty;
            using (var reader = new StreamReader(file.Stream))
            {
                text = reader.ReadToEnd();
            }

            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var pubspec = deserializer.Deserialize<Pubspec>(text);

            foreach (string key in pubspec.dependencies.Keys)
            {
                foreach (Dependency dependency in dependencies[key])
                {
                    // there are cases that we ommit the dependency
                    // because its version is not valid like for example 
                    // is a relative version instead of an absolute one
                    // because of that there are children elements 
                    // that does not contains a entry in the dictionary
                    // those elements should be removed
                    if (components.ContainsKey(dependency.Id))
                    {
                        singleFileComponentRecorder.RegisterUsage(components[dependency.Id], parentComponentId: components[key].Component.Id);
                    }
                }
            }
        }
        
        private bool IsVersionRelative(string version)
        {
            return version.StartsWith("~") || version.StartsWith("=");
        }
    }
}

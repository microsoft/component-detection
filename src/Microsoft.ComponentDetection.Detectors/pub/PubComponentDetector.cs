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

        public override IList<string> SearchPatterns { get; } = new List<string> { "pubspec.lock" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Pub };

        public override int Version { get; } = 1;

        public PubComponentDetector()
        {
            NeedsAutomaticRootDependencyCalculation = true;
        }

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            Logger.LogVerbose("Found Pubspec.lock: " + file.Location);
            ParsePubspecYamlFile(singleFileComponentRecorder, file);

            return Task.CompletedTask;
        }

        private void ParsePubspecYamlFile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream file)
        {
            var components = new Dictionary<string, DetectedComponent>();

            var text = string.Empty;
            using (var reader = new StreamReader(file.Stream))
            {
                text = reader.ReadToEnd();
            }

            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            dynamic pubspec = deserializer.Deserialize<System.Dynamic.ExpandoObject>(text);
            Console.WriteLine(pubspec.packages);

            foreach (dynamic package in pubspec.packages)
            {
                var component = new DetectedComponent(new PubComponent(package.Key, package.Value["version"]));
                singleFileComponentRecorder.RegisterUsage(component, package.Value["dependency"] != "transitive");
            }
        }
        
        private bool IsVersionRelative(string version)
        {
            return version.StartsWith("~") || version.StartsWith("=");
        }
    }
}

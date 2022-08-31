using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Microsoft.ComponentDetection.Detectors.CocoaPods
{
    [Export(typeof(IComponentDetector))]
    public class PodComponentDetector : FileComponentDetector
    {
        public override string Id { get; } = "CocoaPods";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.CocoaPods) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "Podfile.lock" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Pod, ComponentType.Git };

        public override int Version { get; } = 2;

        private class Pod : IYamlConvertible
        {
            public string Name { get; set; }

            public string Version { get; set; }

            public IList<PodDependency> Dependencies { get; set; }

            public string Podspec => this.Name.Split('/', 2)[0];

            public bool IsSubspec => this.Name != this.Podspec;

            public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
            {
                var hasDependencies = parser.Accept<MappingStart>(out _);
                if (hasDependencies)
                {
                    parser.Consume<MappingStart>();
                }

                var podInfo = parser.Consume<Scalar>();
                var components = podInfo.Value.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                this.Name = components[0].Trim();
                this.Version = components[1].Trim();

                if (hasDependencies)
                {
                    this.Dependencies = (IList<PodDependency>)nestedObjectDeserializer(typeof(IList<PodDependency>));

                    parser.Consume<MappingEnd>();
                }
                else
                {
                    this.Dependencies = Array.Empty<PodDependency>();
                }
            }

            public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
            {
                throw new NotImplementedException();
            }
        }

        private class PodDependency : IYamlConvertible
        {
            public string PodName { get; set; }

            public string PodVersion { get; set; }

            public string Podspec => this.PodName.Split('/', 2)[0];

            public bool IsSubspec => this.PodName != this.Podspec;

            public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
            {
                var scalar = parser.Consume<Scalar>();
                var components = scalar.Value.Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                this.PodName = components[0].Trim();
                this.PodVersion = components.Length > 1 ? components[1].Trim() : null;
            }

            public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
            {
                throw new NotImplementedException();
            }
        }

        private class PodfileLock
        {
            [YamlMember(Alias = "PODFILE CHECKSUM", ApplyNamingConventions = false)]
            public string Checksum { get; set; }

            [YamlMember(Alias = "COCOAPODS", ApplyNamingConventions = false)]
            public string CocoapodsVersion { get; set; }

            [YamlMember(Alias = "DEPENDENCIES", ApplyNamingConventions = false)]
            public IList<PodDependency> Dependencies { get; set; }

            [YamlMember(Alias = "SPEC REPOS", ApplyNamingConventions = false)]
            public IDictionary<string, IList<string>> PodspecRepositories { get; set; }

            [YamlMember(Alias = "SPEC CHECKSUMS", ApplyNamingConventions = false)]
            public IDictionary<string, string> PodspecChecksums { get; set; }

            [YamlMember(Alias = "EXTERNAL SOURCES", ApplyNamingConventions = false)]
            public IDictionary<string, IDictionary<string, string>> ExternalSources { get; set; }

            [YamlMember(Alias = "CHECKOUT OPTIONS", ApplyNamingConventions = false)]
            public IDictionary<string, IDictionary<string, string>> CheckoutOptions { get; set; }

            [YamlMember(Alias = "PODS", ApplyNamingConventions = false)]
            public IList<Pod> Pods { get; set; }

            public PodfileLock()
            {
                this.Dependencies = Array.Empty<PodDependency>();
                this.PodspecRepositories = new Dictionary<string, IList<string>>();
                this.PodspecChecksums = new Dictionary<string, string>();
                this.ExternalSources = new Dictionary<string, IDictionary<string, string>>();
                this.CheckoutOptions = new Dictionary<string, IDictionary<string, string>>();
                this.Pods = Array.Empty<Pod>();
            }

            public string GetSpecRepositoryOfSpec(string specName)
            {
                foreach (var repository in this.PodspecRepositories)
                {
                    if (repository.Value.Contains(specName))
                    {
                        // CocoaPods specs are stored in a git repo but depending on settings/CocoaPods version
                        // the repo is shown differently in the Podfile.lock
                        switch (repository.Key.ToLowerInvariant())
                        {
                            case "trunk":
                            case "https://github.com/cocoapods/specs.git":
                                return "trunk";

                            default:
                                return repository.Key;
                        }
                    }
                }

                return null;
            }
        }

        private static async Task<PodfileLock> ParsePodfileLock(IComponentStream file)
        {
            var fileContent = await new StreamReader(file.Stream).ReadToEndAsync();
            var input = new StringReader(fileContent);
            var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

            return deserializer.Deserialize<PodfileLock>(input);
        }

        private static (Pod pod, string key, DetectedComponent detectedComponent)[] ReadPodfileLock(PodfileLock podfileLock)
        {
            return podfileLock.Pods.Select(pod =>
            {
                // Find the spec repository URL for this pod
                var specRepository = podfileLock.GetSpecRepositoryOfSpec(pod.Podspec) ?? string.Empty;

                // Check if the Podspec comes from a git repository or not
                TypedComponent typedComponent;
                string key;
                if (podfileLock.CheckoutOptions.TryGetValue(pod.Podspec, out var checkoutOptions)
                    && checkoutOptions.TryGetValue(":git", out var gitOption)
                    && checkoutOptions.TryGetValue(":commit", out var commitOption))
                {
                    // Create the Git component
                    gitOption = NormalizePodfileGitUri(gitOption);
                    typedComponent = new GitComponent(new Uri(gitOption), commitOption);
                    key = $"{commitOption}@{gitOption}";
                }
                else
                {
                    // Create the Pod component
                    typedComponent = new PodComponent(pod.Podspec, pod.Version, specRepository);
                    key = $"{pod.Podspec}:{pod.Version}@{specRepository}";
                }

                var detectedComponent = new DetectedComponent(typedComponent);

                return (pod, key, detectedComponent);
            })
            .ToArray();
        }

        private static string NormalizePodfileGitUri(string gitOption)
        {
            // Podfiles can be built using git@ references to .git files, but this is not a valid Uri
            // schema. Normalize to https:// so Uri creation doesn't fail
            if (gitOption.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://{gitOption[4..]}";
            }

            return gitOption;
        }

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            this.Logger.LogVerbose($"Found {file.Pattern}: {file.Location}");

            try
            {
                var podfileLock = await ParsePodfileLock(file);

                this.ProcessPodfileLock(singleFileComponentRecorder, podfileLock);
            }
            catch (Exception e)
            {
                this.Logger.LogFailedReadingFile(file.Location, e);
            }
        }

        private void ProcessPodfileLock(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            PodfileLock podfileLock)
        {
            if (podfileLock.Pods.Count == 0)
            {
                return;
            }

            // Create a set of root podspecs
            var rootPodspecs = new HashSet<string>(podfileLock.Dependencies.Select(p => p.Podspec));

            var podKeyDetectedComponents = ReadPodfileLock(podfileLock);
            var rootComponents = new Dictionary<string, DetectedComponent>();
            var nonRootComponents = new Dictionary<string, DetectedComponent>();

            foreach (var (pod, key, detectedComponent) in podKeyDetectedComponents)
            {
                // Check if the pod is a root component and add it to the list of discovered components
                if (rootPodspecs.Contains(pod.Podspec))
                {
                    if (nonRootComponents.TryGetValue(key, out var existingComponent))
                    {
                        rootComponents.TryAdd(key, existingComponent);
                        nonRootComponents.Remove(key);
                    }
                    else
                    {
                        rootComponents.TryAdd(key, detectedComponent);
                    }
                }
                else if (!rootComponents.ContainsKey(key))
                {
                    nonRootComponents.TryAdd(key, detectedComponent);
                }
                else
                {
                    // Ignore current element, it will be recovered later via a root or non-root!
                }
            }

            var dependenciesMap = new Dictionary<string, HashSet<string>>();

            // Map a pod ID to the list of the pod's dependencies
            var podDependencies = new Dictionary<string, List<PodDependency>>();

            // Map a podspec to the pod ID
            var podSpecs = new Dictionary<string, string>();

            foreach (var pod in podfileLock.Pods)
            {
                // Find the spec repository URL for this pod
                var specRepository = podfileLock.GetSpecRepositoryOfSpec(pod.Podspec) ?? string.Empty;

                // Check if the Podspec comes from a git repository or not
                TypedComponent typedComponent;
                string key;
                if (podfileLock.CheckoutOptions.TryGetValue(pod.Podspec, out var checkoutOptions)
                    && checkoutOptions.TryGetValue(":git", out var gitOption)
                    && checkoutOptions.TryGetValue(":commit", out var commitOption))
                {
                    // Create the Git component
                    gitOption = NormalizePodfileGitUri(gitOption);
                    typedComponent = new GitComponent(new Uri(gitOption), commitOption);
                    key = $"{commitOption}@{gitOption}";
                }
                else
                {
                    // Create the Pod component
                    typedComponent = new PodComponent(pod.Podspec, pod.Version, specRepository);
                    key = $"{pod.Podspec}:{pod.Version}@{specRepository}";
                }

                var detectedComponent = new DetectedComponent(typedComponent)
                {
                    DependencyRoots = new HashSet<TypedComponent>(new ComponentComparer()),
                };

                // Check if the pod is a root component and add it to the list of discovered components
                if (rootPodspecs.Contains(pod.Podspec))
                {
                    if (nonRootComponents.TryGetValue(key, out var existingComponent))
                    {
                        rootComponents.TryAdd(key, existingComponent);
                        nonRootComponents.Remove(key);
                    }
                    else
                    {
                        rootComponents.TryAdd(key, detectedComponent);
                    }
                }
                else if (!rootComponents.ContainsKey(key))
                {
                    nonRootComponents.TryAdd(key, detectedComponent);
                }

                // Update the podspec map
                podSpecs.TryAdd(pod.Podspec, key);

                // Update the pod dependencies map
                if (podDependencies.TryGetValue(key, out var dependencies))
                {
                    dependencies.AddRange(pod.Dependencies);
                }
                else
                {
                    podDependencies.TryAdd(key, new List<PodDependency>(pod.Dependencies));
                }
            }

            foreach (var pod in podDependencies)
            {
                // Add all the dependencies to the map, without duplicates
                dependenciesMap.TryAdd(pod.Key, new HashSet<string>());

                foreach (var dependency in pod.Value)
                {
                    if (podSpecs.TryGetValue(dependency.Podspec, out var dependencyKey))
                    {
                        if (dependencyKey != pod.Key)
                        {
                            var temp = podSpecs[dependency.Podspec];
                            dependenciesMap[pod.Key].Add(podSpecs[dependency.Podspec]);
                        }
                    }
                    else
                    {
                        this.Logger.LogWarning($"Missing podspec declaration. podspec={dependency.Podspec}, version={dependency.PodVersion}");
                    }
                }
            }

            foreach (var rootComponent in rootComponents)
            {
                singleFileComponentRecorder.RegisterUsage(
                    rootComponent.Value,
                    isExplicitReferencedDependency: true);

                // Check if this component has any dependencies
                if (!dependenciesMap.ContainsKey(rootComponent.Key))
                {
                    continue;
                }

                // Traverse the dependencies graph for this component and stop if there is a cycle
                // or if we find another root component
                var dependencies = new Queue<string>(dependenciesMap[rootComponent.Key]);
                while (dependencies.Count > 0)
                {
                    var dependency = dependencies.Dequeue();

                    if (rootComponents.TryGetValue(dependency, out var detectedRootComponent))
                    {
                        // Found another root component
                        singleFileComponentRecorder.RegisterUsage(
                            detectedRootComponent,
                            isExplicitReferencedDependency: true,
                            parentComponentId: rootComponent.Value.Component.Id);
                    }
                    else if (nonRootComponents.TryGetValue(dependency, out var detectedComponent))
                    {
                        singleFileComponentRecorder.RegisterUsage(
                            detectedComponent,
                            isExplicitReferencedDependency: true,
                            parentComponentId: rootComponent.Value.Component.Id);

                        // Add the new dependecies to the queue
                        if (dependenciesMap.TryGetValue(dependency, out var newDependencies))
                        {
                            newDependencies.ToList().ForEach(dependencies.Enqueue);
                        }
                        else
                        {
                            // Do nothing!
                        }
                    }
                    else
                    {
                        // Do nothing!
                    }
                }
            }

            foreach (var component in nonRootComponents)
            {
                singleFileComponentRecorder.RegisterUsage(
                    component.Value,
                    isExplicitReferencedDependency: true);
            }
        }
    }
}

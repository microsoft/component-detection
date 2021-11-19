using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Detectors;

namespace Microsoft.ComponentDetection.Orchestrator.Services
{
    // Note : This class isn't unit testable in a meaningful way. Be careful when making changes that you're sure you can test them manually. This class should remain very simple to help prevent future bugs.
    [Export(typeof(IDetectorRegistryService))]
    [Shared]
    public class DetectorRegistryService : ServiceBase, IDetectorRegistryService
    {
        [Import]
        public IDetectorDependencies DetectorDependencies { get; set; }

        private IEnumerable<IComponentDetector> ComponentDetectors { get; set; }

        public IEnumerable<IComponentDetector> GetDetectors(IEnumerable<DirectoryInfo> additionalSearchDirectories, IEnumerable<string> extraDetectorAssemblies)
        {
            var executableLocation = Assembly.GetEntryAssembly().Location;
            var searchPath = Path.Combine(Path.GetDirectoryName(executableLocation), "Plugins");

            List<DirectoryInfo> directoriesToSearch = new List<DirectoryInfo> { new DirectoryInfo(searchPath) };

            if (additionalSearchDirectories != null)
            {
                directoriesToSearch.AddRange(additionalSearchDirectories);
            }

            ComponentDetectors = GetComponentDetectors(directoriesToSearch, extraDetectorAssemblies);

            if (!ComponentDetectors.Any())
            {
                Logger.LogError($"No component detectors were found in {searchPath} or other provided search paths.");
            }

            return ComponentDetectors;
        }

        public IEnumerable<IComponentDetector> GetDetectors(Assembly assemblyToSearch, IEnumerable<string> extraDetectorAssemblies)
        {
            Logger.LogInfo($"Attempting to load component detectors from {assemblyToSearch.FullName}");

            var loadedDetectors = LoadComponentDetectorsFromAssemblies(new List<Assembly> { assemblyToSearch }, extraDetectorAssemblies);

            var pluralPhrase = loadedDetectors.Count == 1 ? "detector was" : "detectors were";
            Logger.LogInfo($"{loadedDetectors.Count()} {pluralPhrase} found in {assemblyToSearch.FullName}\n");

            return loadedDetectors;
        }

        private IList<IComponentDetector> GetComponentDetectors(IEnumerable<DirectoryInfo> searchPaths, IEnumerable<string> extraDetectorAssemblies)
        {
            List<IComponentDetector> detectors = new List<IComponentDetector>();

            using (var record = new LoadComponentDetectorsTelemetryRecord())
            {
                Logger.LogInfo($"Attempting to load default detectors");

                var assembly = Assembly.GetAssembly(typeof(IComponentGovernanceOwnedDetectors));

                var loadedDetectors = LoadComponentDetectorsFromAssemblies(new[] { assembly }, extraDetectorAssemblies);

                var pluralPhrase = loadedDetectors.Count == 1 ? "detector was" : "detectors were";
                Logger.LogInfo($"{loadedDetectors.Count()} {pluralPhrase} found in {assembly.GetName().Name}\n");

                detectors.AddRange(loadedDetectors);

                record.DetectorIds = string.Join(",", loadedDetectors.Select(x => x.Id));
            }

            foreach (var searchPath in searchPaths)
            {
                if (!searchPath.Exists)
                {
                    Logger.LogWarning($"Provided search path {searchPath.FullName} does not exist.");
                    continue;
                }

                using var record = new LoadComponentDetectorsTelemetryRecord();

                Logger.LogInfo($"Attempting to load component detectors from {searchPath}");

                var assemblies = SafeLoadAssemblies(searchPath.GetFiles("*.dll", SearchOption.AllDirectories).Select(x => x.FullName));

                var loadedDetectors = LoadComponentDetectorsFromAssemblies(assemblies, extraDetectorAssemblies);

                var pluralPhrase = loadedDetectors.Count == 1 ? "detector was" : "detectors were";
                Logger.LogInfo($"{loadedDetectors.Count()} {pluralPhrase} found in {searchPath}\n");

                detectors.AddRange(loadedDetectors);

                record.DetectorIds = string.Join(",", loadedDetectors.Select(x => x.Id));
            }

            return detectors;
        }

        private IList<IComponentDetector> LoadComponentDetectorsFromAssemblies(IEnumerable<Assembly> assemblies, IEnumerable<string> extraDetectorAssemblies)
        {
            new InjectionParameters(DetectorDependencies);
            var configuration = new ContainerConfiguration()
                .WithAssemblies(assemblies);

            foreach (var detectorAssemblyPath in extraDetectorAssemblies)
            {
                var detectorAssembly = Assembly.LoadFrom(detectorAssemblyPath);
                var detectorTypes = detectorAssembly.GetTypes().Where(x => typeof(IComponentDetector).IsAssignableFrom(x));
                foreach (var detectorType in detectorTypes)
                {
                    configuration = configuration.WithPart(detectorType);
                }
            }

            configuration = configuration
                .WithPart(typeof(InjectionParameters));

            using var container = configuration.CreateContainer();

            return container.GetExports<IComponentDetector>().ToList();
        }

        // Plugin producers may include files we have already loaded
        private IList<Assembly> SafeLoadAssemblies(IEnumerable<string> files)
        {
            List<Assembly> assemblyList = new List<Assembly>();

            foreach (var file in files)
            {
                try
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);

                    assemblyList.Add(assembly);
                }
                catch (FileLoadException ex)
                {
                    if (ex.Message == "Assembly with same name is already loaded")
                    {
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return assemblyList;
        }
    }
}

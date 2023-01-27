namespace Microsoft.ComponentDetection.Orchestrator.Services;
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

// Note : This class isn't unit testable in a meaningful way. Be careful when making changes that you're sure you can test them manually. This class should remain very simple to help prevent future bugs.
[Export(typeof(IDetectorRegistryService))]
[Shared]
public class DetectorRegistryService : ServiceBase, IDetectorRegistryService
{
    [Import]
    public IDetectorDependencies DetectorDependencies { get; set; }

    private IEnumerable<IComponentDetector> ComponentDetectors { get; set; }

    public IEnumerable<IComponentDetector> GetDetectors(IEnumerable<DirectoryInfo> additionalSearchDirectories, IEnumerable<string> extraDetectorAssemblies, bool skipPluginsDirectory = false)
    {
        var directoriesToSearch = new List<DirectoryInfo>();

        // Checking Plugins directory is not required when skip argument is provided
        if (!skipPluginsDirectory)
        {
            var executableLocation = Assembly.GetEntryAssembly().Location;
            var searchPath = Path.Combine(Path.GetDirectoryName(executableLocation), "Plugins");
            directoriesToSearch.Add(new DirectoryInfo(searchPath));
        }

        if (additionalSearchDirectories != null)
        {
            directoriesToSearch.AddRange(additionalSearchDirectories);
        }

        this.ComponentDetectors = this.GetComponentDetectors(directoriesToSearch, extraDetectorAssemblies);

        if (!this.ComponentDetectors.Any())
        {
            this.Logger.LogError($"No component detectors were found in {directoriesToSearch.FirstOrDefault()} or other provided search paths.");
        }

        return this.ComponentDetectors;
    }

    public IEnumerable<IComponentDetector> GetDetectors(Assembly assemblyToSearch, IEnumerable<string> extraDetectorAssemblies)
    {
        this.Logger.LogInfo($"Attempting to load component detectors from {assemblyToSearch.FullName}");

        var loadedDetectors = this.LoadComponentDetectorsFromAssemblies(new List<Assembly> { assemblyToSearch }, extraDetectorAssemblies);

        var pluralPhrase = loadedDetectors.Count == 1 ? "detector was" : "detectors were";
        this.Logger.LogInfo($"{loadedDetectors.Count} {pluralPhrase} found in {assemblyToSearch.FullName}");

        return loadedDetectors;
    }

    private IList<IComponentDetector> GetComponentDetectors(IEnumerable<DirectoryInfo> searchPaths, IEnumerable<string> extraDetectorAssemblies)
    {
        var detectors = new List<IComponentDetector>();

        using (var record = new LoadComponentDetectorsTelemetryRecord())
        {
            this.Logger.LogInfo($"Attempting to load default detectors");

            var assembly = Assembly.GetAssembly(typeof(IComponentGovernanceOwnedDetectors));

            var loadedDetectors = this.LoadComponentDetectorsFromAssemblies(new[] { assembly }, extraDetectorAssemblies);

            var pluralPhrase = loadedDetectors.Count == 1 ? "detector was" : "detectors were";
            this.Logger.LogInfo($"{loadedDetectors.Count} {pluralPhrase} found in {assembly.GetName().Name}\n");

            detectors.AddRange(loadedDetectors);

            record.DetectorIds = string.Join(",", loadedDetectors.Select(x => x.Id));
        }

        foreach (var searchPath in searchPaths)
        {
            if (!searchPath.Exists)
            {
                this.Logger.LogWarning($"Provided search path {searchPath.FullName} does not exist.");
                continue;
            }

            using var record = new LoadComponentDetectorsTelemetryRecord();

            this.Logger.LogInfo($"Attempting to load component detectors from {searchPath}");

            var assemblies = SafeLoadAssemblies(searchPath.GetFiles("*.dll", SearchOption.AllDirectories).Select(x => x.FullName));

            var loadedDetectors = this.LoadComponentDetectorsFromAssemblies(assemblies, extraDetectorAssemblies);

            var pluralPhrase = loadedDetectors.Count == 1 ? "detector was" : "detectors were";
            this.Logger.LogInfo($"{loadedDetectors.Count} {pluralPhrase} found in {searchPath}\n");

            detectors.AddRange(loadedDetectors);

            record.DetectorIds = string.Join(",", loadedDetectors.Select(x => x.Id));
        }

        return detectors;
    }

    private IList<IComponentDetector> LoadComponentDetectorsFromAssemblies(IEnumerable<Assembly> assemblies, IEnumerable<string> extraDetectorAssemblies)
    {
        new InjectionParameters(this.DetectorDependencies);
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
    private static IList<Assembly> SafeLoadAssemblies(IEnumerable<string> files)
    {
        var assemblyList = new List<Assembly>();

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

namespace Microsoft.ComponentDetection.Orchestrator.Services;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.ComponentDetection.Contracts;

public interface IDetectorRegistryService
{
    IEnumerable<IComponentDetector> GetDetectors(IEnumerable<DirectoryInfo> additionalSearchDirectories, IEnumerable<string> extraDetectorAssemblies, bool skipPluginsDirectory = false);

    IEnumerable<IComponentDetector> GetDetectors(Assembly assemblyToSearch, IEnumerable<string> extraDetectorAssemblies);
}

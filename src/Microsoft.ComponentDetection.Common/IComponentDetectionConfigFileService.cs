namespace Microsoft.ComponentDetection.Common;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Provides methods for writing files.
/// </summary>
public interface IComponentDetectionConfigFileService
{
    /// <summary>
    /// Initializes the Component detection config service.
    /// Checks the following for the presence of a config file:
    /// 1. The environment variable "ComponentDetection.ConfigFilePath" and the path exists
    /// 2. If there is a file present at the root directory named "ComponentDetection.yml".
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InitAsync(string explicitConfigPath, string rootDirectoryPath = null);

    /// <summary>
    /// Retrieves the merged config files.
    /// </summary>
    /// <returns>The ComponentDetection config file as an object.</returns>
    ComponentDetectionConfigFile GetComponentDetectionConfig();
}

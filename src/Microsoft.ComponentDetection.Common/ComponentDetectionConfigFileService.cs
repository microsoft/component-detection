namespace Microsoft.ComponentDetection.Common;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

/// <inheritdoc />
public class ComponentDetectionConfigFileService : IComponentDetectionConfigFileService
{
    private readonly IFileUtilityService fileUtilityService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly ComponentDetectionConfigFile componentDetectionConfig;
    private readonly ILogger<FastDirectoryWalkerFactory> logger;
    private bool serviceInitComplete;

    public ComponentDetectionConfigFileService(
        IFileUtilityService fileUtilityService,
        IPathUtilityService pathUtilityService,
        ILogger<FastDirectoryWalkerFactory> logger)
    {
        this.fileUtilityService = fileUtilityService;
        this.pathUtilityService = pathUtilityService;
        this.logger = logger;
        this.componentDetectionConfig = new ComponentDetectionConfigFile();
        this.serviceInitComplete = false;
    }

    public ComponentDetectionConfigFile GetComponentDetectionConfig()
    {
        this.EnsureInit();
        return this.componentDetectionConfig;
    }

    public async Task InitAsync(string explicitConfigPath, string rootDirectoryPath = null)
    {
        if (!string.IsNullOrEmpty(rootDirectoryPath))
        {
            await this.LoadComponentDetectionConfigFilesFromRootDirectoryAsync(rootDirectoryPath);
        }

        this.serviceInitComplete = true;
    }

    private async Task LoadComponentDetectionConfigFilesFromRootDirectoryAsync(string rootDirectoryPath)
    {
        var workingDir = this.pathUtilityService.NormalizePath(rootDirectoryPath);

        var reportFile = new FileInfo(Path.Combine(workingDir, "ComponentDetection.yml"));
        if (this.fileUtilityService.Exists(reportFile.FullName))
        {
            await this.LoadComponentDetectionConfigAsync(reportFile.FullName);
        }
    }

    private async Task LoadComponentDetectionConfigAsync(string configFile)
    {
        if (!this.fileUtilityService.Exists(configFile))
        {
            throw new InvalidOperationException($"Attempted to load non-existant ComponentDetectionConfig file: {configFile}");
        }

        var configFileInfo = new FileInfo(configFile);
        var fileContents = await this.fileUtilityService.ReadAllTextAsync(configFileInfo);
        var newConfig = this.ParseComponentDetectionConfig(fileContents);
        this.logger.LogInformation("Loaded component detection config file from {ConfigFile}", configFile);
    }

    /// <summary>
    /// Reads the component detection config from a file path.
    /// </summary>
    /// <param name="configFileContent">The string contents of the config yaml file.</param>
    /// <returns>The ComponentDetection config file as an object.</returns>
    private ComponentDetectionConfigFile ParseComponentDetectionConfig(string configFileContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<ComponentDetectionConfigFile>(new StringReader(configFileContent));
    }

    private void EnsureInit()
    {
        if (!this.serviceInitComplete)
        {
            throw new InvalidOperationException("ComponentDetection config files have not been loaded yet!");
        }
    }
}

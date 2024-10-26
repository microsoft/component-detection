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
    private const string ComponentDetectionConfigFileEnvVar = "ComponentDetection.ComponentDetectionConfigFile";
    private readonly IFileUtilityService fileUtilityService;
    private readonly IEnvironmentVariableService environmentVariableService;
    private readonly IPathUtilityService pathUtilityService;
    private readonly ComponentDetectionConfigFile componentDetectionConfig;
    private readonly ILogger<FastDirectoryWalkerFactory> logger;
    private bool serviceInitComplete;

    public ComponentDetectionConfigFileService(
        IFileUtilityService fileUtilityService,
        IEnvironmentVariableService environmentVariableService,
        IPathUtilityService pathUtilityService,
        ILogger<FastDirectoryWalkerFactory> logger)
    {
        this.fileUtilityService = fileUtilityService;
        this.pathUtilityService = pathUtilityService;
        this.environmentVariableService = environmentVariableService;
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
        await this.LoadFromEnvironmentVariableAsync();
        if (!string.IsNullOrEmpty(explicitConfigPath))
        {
            await this.LoadComponentDetectionConfigAsync(explicitConfigPath);
        }

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

    private async Task LoadFromEnvironmentVariableAsync()
    {
        if (this.environmentVariableService.DoesEnvironmentVariableExist(ComponentDetectionConfigFileEnvVar))
        {
            var possibleConfigFilePath = this.environmentVariableService.GetEnvironmentVariable(ComponentDetectionConfigFileEnvVar);
            if (this.fileUtilityService.Exists(possibleConfigFilePath))
            {
                await this.LoadComponentDetectionConfigAsync(possibleConfigFilePath);
            }
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
        this.MergeComponentDetectionConfig(newConfig);
        this.logger.LogInformation("Loaded component detection config file from {ConfigFile}", configFile);
    }

    /// <summary>
    /// Merges two component detection configs, giving precedence to values already set in the first file.
    /// </summary>
    /// <param name="newConfig">The new config file to be merged into the existing config set.</param>
    private void MergeComponentDetectionConfig(ComponentDetectionConfigFile newConfig)
    {
        foreach ((var name, var value) in newConfig.Variables)
        {
            if (!this.componentDetectionConfig.Variables.ContainsKey(name))
            {
                this.componentDetectionConfig.Variables[name] = value;
            }
        }
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

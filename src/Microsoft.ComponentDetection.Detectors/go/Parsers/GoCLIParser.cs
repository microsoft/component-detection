#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class GoCLIParser : IGoParser
{
    private readonly ILogger logger;
    private readonly IFileUtilityService fileUtilityService;
    private readonly ICommandLineInvocationService commandLineInvocationService;

    public GoCLIParser(ILogger logger, IFileUtilityService fileUtilityService, ICommandLineInvocationService commandLineInvocationService)
    {
        this.logger = logger;
        this.fileUtilityService = fileUtilityService;
        this.commandLineInvocationService = commandLineInvocationService;
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "False positive")]
    public async Task<bool> ParseAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file,
        GoGraphTelemetryRecord record)
    {
        record.WasGraphSuccessful = false;
        record.DidGoCliCommandFail = false;
        var projectRootDirectory = Directory.GetParent(file.Location);
        record.ProjectRoot = projectRootDirectory.FullName;

        var isGoAvailable = await this.commandLineInvocationService.CanCommandBeLocatedAsync("go", null, workingDirectory: projectRootDirectory, ["version"]);
        record.IsGoAvailable = isGoAvailable;

        if (!isGoAvailable)
        {
            this.logger.LogInformation("Go CLI was not found in the system");
            return false;
        }

        this.logger.LogInformation("Go CLI was found in system and will be used to generate dependency graph. " +
                                   "Detection time may be improved by activating fallback strategy (https://github.com/microsoft/component-detection/blob/main/docs/detectors/go.md#fallback-detection-strategy). " +
                                   "But, it will introduce noise into the detected components.");

        var goDependenciesProcess = await this.commandLineInvocationService.ExecuteCommandAsync("go", null, workingDirectory: projectRootDirectory, ["list", "-mod=readonly", "-m", "-json", "all"]);
        if (goDependenciesProcess.ExitCode != 0)
        {
            this.logger.LogError("Go CLI command \"go list -m -json all\" failed with error: {GoDependenciesProcessStdErr}", goDependenciesProcess.StdErr);
            this.logger.LogError("Go CLI could not get dependency build list at location: {Location}. Fallback go.sum/go.mod parsing will be used.", file.Location);
            record.DidGoCliCommandFail = true;
            record.GoCliCommandError = goDependenciesProcess.StdErr;
            return false;
        }

        this.RecordBuildDependencies(goDependenciesProcess.StdOut, singleFileComponentRecorder, projectRootDirectory.FullName);

        record.WasGraphSuccessful = await GoDependencyGraphUtility.GenerateAndPopulateDependencyGraphAsync(
            this.commandLineInvocationService,
            this.logger,
            singleFileComponentRecorder,
            projectRootDirectory.FullName,
            record);

        return true;
    }

    private void RecordBuildDependencies(string goListOutput, ISingleFileComponentRecorder singleFileComponentRecorder, string projectRootDirectoryFullName)
    {
        var goBuildModules = new List<GoBuildModule>();
        var reader = new JsonTextReader(new StringReader(goListOutput))
        {
            SupportMultipleContent = true,
        };

        using var record = new GoReplaceTelemetryRecord();

        while (reader.Read())
        {
            var serializer = new JsonSerializer();
            var buildModule = serializer.Deserialize<GoBuildModule>(reader);

            goBuildModules.Add(buildModule);
        }

        foreach (var dependency in goBuildModules)
        {
            var dependencyName = $"{dependency.Path} {dependency.Version}";

            if (dependency.Main)
            {
                // main is the entry point module (superfluous as we already have the file location)
                continue;
            }

            if (dependency.Replace?.Path != null && dependency.Replace.Version == null)
            {
                var dirName = projectRootDirectoryFullName;
                var combinedPath = Path.Combine(dirName, dependency.Replace.Path, "go.mod");
                var goModFilePath = Path.GetFullPath(combinedPath);
                if (this.fileUtilityService.Exists(goModFilePath))
                {
                    this.logger.LogInformation("go Module {GoModule} is being replaced with module at path {GoModFilePath}", dependencyName, goModFilePath);
                    record.GoModPathAndVersion = dependencyName;
                    record.GoModReplacement = goModFilePath;
                    continue;
                }

                this.logger.LogWarning("go.mod file {GoModFilePath} does not exist in the relative path given for replacement", goModFilePath);
                record.GoModPathAndVersion = goModFilePath;
                record.GoModReplacement = null;
            }

            GoComponent goComponent;
            if (dependency.Replace?.Path != null && dependency.Replace.Version != null)
            {
                var dependencyReplacementName = $"{dependency.Replace.Path} {dependency.Replace.Version}";
                record.GoModPathAndVersion = dependencyName;
                record.GoModReplacement = dependencyReplacementName;
                try
                {
                    goComponent = new GoComponent(dependency.Replace.Path, dependency.Replace.Version);
                    this.logger.LogInformation("go Module {GoModule} being replaced with module {GoModuleReplacement}", dependencyName, dependencyReplacementName);
                }
                catch (Exception ex)
                {
                    record.ExceptionMessage = ex.Message;
                    this.logger.LogWarning("tried to use replace module {GoModuleReplacement} but got this error {ErrorMessage} using original module {GoModule} instead", dependencyReplacementName, ex.Message, dependencyName);
                    goComponent = new GoComponent(dependency.Path, dependency.Version);
                }
            }
            else
            {
                goComponent = new GoComponent(dependency.Path, dependency.Version);
            }

            if (dependency.Indirect)
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent));
            }
            else
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(goComponent), isExplicitReferencedDependency: true);
            }
        }
    }

    private class GoBuildModule
    {
        public string Path { get; set; }

        public bool Main { get; set; }

        public string Version { get; set; }

        public bool Indirect { get; set; }

        public GoBuildModule Replace { get; set; }
    }
}

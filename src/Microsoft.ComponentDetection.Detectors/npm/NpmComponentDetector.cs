namespace Microsoft.ComponentDetection.Detectors.Npm;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.Extensions.Logging;

public class NpmComponentDetector : FileComponentDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public NpmComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<NpmComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "Npm";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Npm)!];

    public override IList<string> SearchPatterns { get; } = ["package.json"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Npm];

    public override int Version { get; } = 3;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var filePath = file.Location;

        try
        {
            var packageJson = await JsonSerializer.DeserializeAsync<PackageJson>(file.Stream, JsonOptions, cancellationToken);
            if (packageJson is null)
            {
                this.Logger.LogInformation("Could not deserialize {PackageJsonFile}", filePath);
                return;
            }

            if (string.IsNullOrWhiteSpace(packageJson.Name) || string.IsNullOrWhiteSpace(packageJson.Version))
            {
                this.Logger.LogInformation("{BadPackageJson} does not contain a name and/or version. These are required fields for a valid package.json file. It and its dependencies will not be registered.", filePath);
                return;
            }

            this.ProcessPackageJson(filePath, singleFileComponentRecorder, packageJson);
        }
        catch (JsonException e)
        {
            this.Logger.LogInformation(e, "Could not parse JSON from file {PackageJsonFilePaths}", filePath);
        }
    }

    protected virtual bool ProcessPackageJson(string filePath, ISingleFileComponentRecorder singleFileComponentRecorder, PackageJson packageJson)
    {
        var name = packageJson.Name!;
        var version = packageJson.Version!;

        if (!SemanticVersion.TryParse(version, out _))
        {
            this.Logger.LogWarning("Unable to parse version {NpmPackageVersion} for package {NpmPackageName} found at path {NpmPackageLocation}. This may indicate an invalid npm package component and it will not be registered.", version, name, filePath);
            singleFileComponentRecorder.RegisterPackageParseFailure($"{name} - {version}");
            return false;
        }

        // Check for VS Code extensions
        // See https://code.visualstudio.com/api/working-with-extensions/publishing-extension#visual-studio-code-compatibility
        var containsVsCodeEngine = false;
        if (packageJson.Engines is not null && packageJson.Engines.ContainsKey("vscode"))
        {
            containsVsCodeEngine = true;
        }

        if (containsVsCodeEngine)
        {
            this.Logger.LogInformation("{NpmPackageName} found at path {NpmPackageLocation} represents a built-in VS Code extension. This package will not be registered.", name, filePath);
            return false;
        }

        var author = this.GetAuthor(packageJson.Author, name, filePath);
        var npmComponent = new NpmComponent(name, version, author: author);

        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(npmComponent));
        return true;
    }

    private NpmAuthor? GetAuthor(PackageJsonAuthor? author, string packageName, string filePath)
    {
        if (author is null || string.IsNullOrEmpty(author.Name))
        {
            return null;
        }

        return new NpmAuthor(author.Name, author.Email);
    }
}

namespace Microsoft.ComponentDetection.Detectors.Npm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.Extensions.Logging;

public static class NpmComponentUtilities
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex UnsafeCharactersRegex = new Regex(
        @"[?<>#%{}|`'^\\~\[\]""\s\x7f]|[\x00-\x1f]|[\x80-\xff]",
        RegexOptions.Compiled);

    public static readonly string NodeModules = "node_modules";
    public static readonly string LockFile3EnvFlag = "CD_LOCKFILE_V3_ENABLED";

    public static void TraverseAndRecordComponents(bool isDevDependency, ISingleFileComponentRecorder singleFileComponentRecorder, TypedComponent component, TypedComponent explicitReferencedDependency, string? parentComponentId = null)
    {
        AddOrUpdateDetectedComponent(singleFileComponentRecorder, component, isDevDependency, parentComponentId, isExplicitReferencedDependency: string.Equals(component.Id, explicitReferencedDependency.Id));
    }

    public static DetectedComponent AddOrUpdateDetectedComponent(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        TypedComponent component,
        bool isDevDependency,
        string? parentComponentId = null,
        bool isExplicitReferencedDependency = false)
    {
        var newComponent = new DetectedComponent(component);
        singleFileComponentRecorder.RegisterUsage(newComponent, isExplicitReferencedDependency, parentComponentId, isDevDependency);
        return singleFileComponentRecorder.GetComponent(component.Id);
    }

    public static TypedComponent? GetTypedComponent(string name, string? version, string? hash, string npmRegistryHost, ILogger logger)
    {
        var moduleName = GetModuleName(name);

        if (!IsPackageNameValid(moduleName))
        {
            logger.LogInformation("The package name {PackageName} is invalid or unsupported and a component will not be recorded.", moduleName);
            return null;
        }

        if (version is null || (!SemanticVersion.TryParse(version, out var result) && !TryParseNpmVersion(npmRegistryHost, moduleName, version, out result)))
        {
            logger.LogInformation("Version string {ComponentVersion} for component {ComponentName} is invalid or unsupported and a component will not be recorded.", version, moduleName);
            return null;
        }

        var versionString = result!.ToString();
        TypedComponent component = new NpmComponent(moduleName, versionString, hash);
        return component;
    }

    public static bool TryParseNpmVersion(
        string npmRegistryHost,
        string packageName,
        string? versionString,
        out SemanticVersion? version
    )
    {
        if (
            versionString is not null
            && Uri.TryCreate(versionString, UriKind.Absolute, out var parsedUri)
            && string.Equals(npmRegistryHost, parsedUri.Host, StringComparison.OrdinalIgnoreCase)
        )
        {
            return TryParseNpmRegistryVersion(packageName, parsedUri, out version);
        }

        version = null;
        return false;
    }

    public static bool TryParseNpmRegistryVersion(string packageName, Uri versionString, out SemanticVersion? version)
    {
        try
        {
            var file = Path.GetFileNameWithoutExtension(versionString.LocalPath);
            var potentialVersion = file.Replace($"{packageName}-", string.Empty);

            return SemanticVersion.TryParse(potentialVersion, out version);
        }
        catch (Exception)
        {
            version = null;
            return false;
        }
    }

    public static IDictionary<string, IDictionary<string, bool>> TryGetAllPackageJsonDependencies(Stream stream, out IList<string> yarnWorkspaces)
    {
        yarnWorkspaces = [];

        var packageJson = JsonSerializer.Deserialize<PackageJson>(stream, JsonOptions);
        if (packageJson is null)
        {
            return new Dictionary<string, IDictionary<string, bool>>();
        }

        if (packageJson.Private == true && packageJson.Workspaces is not null)
        {
            yarnWorkspaces = packageJson.Workspaces.ToList();
        }

        var dependencies = packageJson.Dependencies ?? new Dictionary<string, string>();
        var optionalDependencies = packageJson.OptionalDependencies ?? new Dictionary<string, string>();
        var devDependencies = packageJson.DevDependencies ?? new Dictionary<string, string>();

        var allDependencies = dependencies.Concat(optionalDependencies).ToDictionary(x => x.Key, x => x.Value);

        var returnedDependencies = AttachDevInformationToDependencies(allDependencies, false);
        return returnedDependencies.Concat(AttachDevInformationToDependencies(devDependencies, true)).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);
    }

    /// <summary>
    /// Gets the module name, stripping off the "node_modules/" prefix if it exists.
    /// </summary>
    /// <param name="name">The name of the module.</param>
    /// <returns>The module name, stripped of the "node_modules/" prefix if it exists.</returns>
    public static string GetModuleName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var index = name.LastIndexOf($"{NodeModules}/", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            name = name[(index + $"{NodeModules}/".Length)..];
        }

        return name;
    }

    internal static bool IsPackageNameValid(string name)
    {
        if (Uri.TryCreate(name, UriKind.Absolute, out _))
        {
            return false;
        }

        return !(name.Length >= 214
                 || name.StartsWith('.')
                 || name.StartsWith('_')
                 || UnsafeCharactersRegex.IsMatch(name));
    }

    private static IDictionary<string, IDictionary<string, bool>> AttachDevInformationToDependencies(IDictionary<string, string> dependencies, bool isDev)
    {
        IDictionary<string, IDictionary<string, bool>> returnedDependencies = new Dictionary<string, IDictionary<string, bool>>();

        foreach (var item in dependencies)
        {
            if (!returnedDependencies.ContainsKey(item.Key))
            {
                returnedDependencies[item.Key] = new Dictionary<string, bool>();
            }

            if (returnedDependencies[item.Key].TryGetValue(item.Value, out var wasDev))
            {
                returnedDependencies[item.Key][item.Value] = wasDev && isDev;
            }
            else
            {
                returnedDependencies[item.Key].Add(item.Value, isDev);
            }
        }

        return returnedDependencies;
    }
}

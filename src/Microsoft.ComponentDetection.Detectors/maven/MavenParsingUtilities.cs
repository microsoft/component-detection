#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public static class MavenParsingUtilities
{
    private static readonly Dictionary<string, DependencyScope> MavenScopeToDependencyScopeMapping = new Dictionary<string, DependencyScope>()
    {
        { "compile", DependencyScope.MavenCompile },
        { "provided", DependencyScope.MavenProvided },
        { "system", DependencyScope.MavenSystem },
        { "test", DependencyScope.MavenTest },
        { "runtime", DependencyScope.MavenRuntime },
    };

    public static (DetectedComponent Component, bool? IsDevelopmentDependency, DependencyScope? DependencyScope) GenerateDetectedComponentAndMetadataFromMavenString(string key, ILogger logger = null)
    {
        var (component, isDevDependency, dependencyScope) = GetMavenComponentAndIsDevDependencyAndScope(key, logger);

        var detectedComponent = new DetectedComponent(component);

        return (detectedComponent, isDevDependency, dependencyScope);
    }

    private static (MavenComponent Component, bool? IsDevDependency, DependencyScope? DependencyScope) GetMavenComponentAndIsDevDependencyAndScope(string componentString, ILogger logger = null)
    {
        var (groupId, artifactId, version, isDevelopmentDependency, dependencyScope) = GetMavenComponentStringInfo(componentString, logger);
        return (new MavenComponent(groupId, artifactId, version), isDevelopmentDependency, dependencyScope);
    }

    private static (string GroupId, string ArtifactId, string Version, bool? IsDevelopmentDependency, DependencyScope DependencyScope)
        GetMavenComponentStringInfo(string mavenComponentString, ILogger logger = null)
    {
        var results = mavenComponentString.Split(':');
        if (results.Length > 6 || results.Length < 4)
        {
            throw new InvalidOperationException($"Bad key ('{mavenComponentString}') found in generated dependency graph.");
        }

        if (results.Length == 6)
        {
            // Six part versions have an entry in their 4th index. We remove it to normalize. E.g.:
            // var mysteriousSixPartVersionPart = results[3];
            results = [results[0], results[1], results[2], results[4], results[5]];
        }

        // 'MavenCompile' is a default scope for maven dependencies.
        var dependencyScope = DependencyScope.MavenCompile;
        var groupId = results[0];
        var artifactId = results[1];
        var version = results[3];
        bool? isDevDependency = null;
        if (results.Length == 5)
        {
            dependencyScope = MavenScopeToDependencyScopeMapping.TryGetValue(
                Regex.Match(results[4], @"^([\w]+)").Value,
                out dependencyScope)
                ? dependencyScope
                : HandleUnknownDependencyScope(results[4], mavenComponentString, logger);
            isDevDependency = dependencyScope == DependencyScope.MavenTest;
        }

        return (groupId, artifactId, version, isDevDependency, dependencyScope);
    }

    private static DependencyScope HandleUnknownDependencyScope(string invalidScope, string mavenComponentString, ILogger logger = null)
    {
        logger?.LogInformation("Invalid scope ('{InvalidScope}') found for '{MavenComponentString}' in generated dependency graph. Replacing it with 'Compile' scope.", invalidScope, mavenComponentString);
        return DependencyScope.MavenCompile;
    }
}

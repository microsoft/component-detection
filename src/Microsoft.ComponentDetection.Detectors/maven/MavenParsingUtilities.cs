namespace Microsoft.ComponentDetection.Detectors.Maven;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

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

    public static (DetectedComponent Component, bool? IsDevelopmentDependency, DependencyScope? DependencyScope) GenerateDetectedComponentAndMetadataFromMavenString(string key)
    {
        var (component, isDevDependency, dependencyScope) = GetMavenComponentAndIsDevDependencyAndScope(key);

        var detectedComponent = new DetectedComponent(component);

        return (detectedComponent, isDevDependency, dependencyScope);
    }

    private static (MavenComponent Component, bool? IsDevDependency, DependencyScope? DependencyScope) GetMavenComponentAndIsDevDependencyAndScope(string componentString)
    {
        var (groupId, artifactId, version, isDevelopmentDependency, dependencyScope) = GetMavenComponentStringInfo(componentString);
        return (new MavenComponent(groupId, artifactId, version), isDevelopmentDependency, dependencyScope);
    }

    private static (string GroupId, string ArtifactId, string Version, bool? IsDevelopmentDependency, DependencyScope DependencyScope)
        GetMavenComponentStringInfo(string mavenComponentString)
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
            results = new[] { results[0], results[1], results[2], results[4], results[5] };
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
                : throw new InvalidOperationException($"Invalid scope ('{results[4]}') found for '{mavenComponentString}' found in generated dependency graph.");
            isDevDependency = dependencyScope == DependencyScope.MavenTest;
        }

        return (groupId, artifactId, version, isDevDependency, dependencyScope);
    }
}

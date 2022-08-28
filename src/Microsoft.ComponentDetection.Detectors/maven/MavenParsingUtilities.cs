namespace Microsoft.ComponentDetection.Detectors.Maven
{
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

        public static (DetectedComponent Component, bool? IsDevelopmentDependency, DependencyScope? dependencyScope) GenerateDetectedComponentAndMetadataFromMavenString(string key)
        {
            var componentAndMetaData = GetMavenComponentAndIsDevDependencyAndScope(key);

            var detectedComponent = new DetectedComponent(componentAndMetaData.component);

            return (detectedComponent, componentAndMetaData.isDevDependency, componentAndMetaData.dependencyScope);
        }

        private static (MavenComponent component, bool? isDevDependency, DependencyScope? dependencyScope) GetMavenComponentAndIsDevDependencyAndScope(string componentString)
        {
            var info = GetMavenComponentStringInfo(componentString);
            return (new MavenComponent(info.groupId, info.artifactId, info.version), info.isDevelopmentDependency, info.dependencyScope);
        }

        private static (string groupId, string artifactId, string version, bool? isDevelopmentDependency, DependencyScope dependencyScope)
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
}

using System;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Detectors.Maven
{
    public static class MavenParsingUtilities
    {
        public static (DetectedComponent Component, bool? IsDevelopmentDependency) GenerateDetectedComponentFromMavenString(string key)
        {
            var component = GetMavenComponentFromComponentString(key);

            var detectedComponent = new DetectedComponent(component.component);

            return (detectedComponent, component.isDevDependency);
        }

        private static (MavenComponent component, bool? isDevDependency) GetMavenComponentFromComponentString(string componentString)
        {
            var info = GetMavenComponentStringInfo(componentString);
            return (new MavenComponent(info.groupId, info.artifactId, info.version), info.isDevelopmentDependency);
        }

        private static (string groupId, string artifactId, string version, bool? isDevelopmentDependency)
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

            var groupId = results[0];
            var artifactId = results[1];
            var version = results[3];
            bool? isDevDependency = null;
            if (results.Length == 5)
            {
                isDevDependency = string.Equals(results[4], "test", StringComparison.OrdinalIgnoreCase);
            }

            return (groupId, artifactId, version, isDevDependency);
        }
    }
}

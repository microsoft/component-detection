using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace Microsoft.ComponentDetection.Detectors.Npm
{
    public static class NpmComponentUtilities
    {
        private static readonly Regex UnsafeCharactersRegex = new Regex(
            @"[?<>#%{}|`'^\\~\[\]""\s\x7f]|[\x00-\x1f]|[\x80-\xff]",
            RegexOptions.Compiled);

        public static void TraverseAndRecordComponents(JProperty currentDependency, ISingleFileComponentRecorder singleFileComponentRecorder, TypedComponent component, TypedComponent explicitReferencedDependency, string parentComponentId = null)
        {
            var devJValue = currentDependency.Value["dev"] as JValue;
            var isDevDependency = devJValue == null ? false : (bool)devJValue;
            AddOrUpdateDetectedComponent(singleFileComponentRecorder, component, isDevDependency, parentComponentId, isExplicitReferencedDependency: string.Equals(component.Id, explicitReferencedDependency.Id));
        }

        public static DetectedComponent AddOrUpdateDetectedComponent(
            ISingleFileComponentRecorder singleFileComponentRecorder,
            TypedComponent component,
            bool isDevDependency,
            string parentComponentId = null,
            bool isExplicitReferencedDependency = false)
        {
            var newComponent = new DetectedComponent(component);
            singleFileComponentRecorder.RegisterUsage(newComponent, isExplicitReferencedDependency, parentComponentId, isDevDependency);
            return singleFileComponentRecorder.GetComponent(component.Id);
        }

        public static TypedComponent GetTypedComponent(JProperty currentDependency, string npmRegistryHost, ILogger logger)
        {
            var name = currentDependency.Name;
            var version = currentDependency.Value["version"].ToString();
            var hash = currentDependency.Value["integrity"]?.ToString(); // https://docs.npmjs.com/configuring-npm/package-lock-json.html#integrity

            if (!IsPackageNameValid(name))
            {
                logger.LogInfo($"The package name {name} is invalid or unsupported and a component will not be recorded.");
                return null;
            }

            if (!SemanticVersion.TryParse(version, out var result) && !TryParseNpmVersion(npmRegistryHost, name, version, out result))
            {
                logger.LogInfo($"Version string {version} for component {name} is invalid or unsupported and a component will not be recorded.");
                return null;
            }

            version = result.ToString();
            TypedComponent component = new NpmComponent(name, version, hash);
            return component;
        }

        public static bool TryParseNpmVersion(string npmRegistryHost, string packageName, string versionString, out SemanticVersion version)
        {
            if (Uri.TryCreate(versionString, UriKind.Absolute, out var parsedUri))
            {
                if (string.Equals(npmRegistryHost, parsedUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    return TryParseNpmRegistryVersion(packageName, parsedUri, out version);
                }
            }

            version = null;
            return false;
        }

        public static bool TryParseNpmRegistryVersion(string packageName, Uri versionString, out SemanticVersion version)
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
            yarnWorkspaces = new List<string>();

            using var file = new StreamReader(stream);
            using var reader = new JsonTextReader(file);

            IDictionary<string, string> dependencies = new Dictionary<string, string>();
            IDictionary<string, string> devDependencies = new Dictionary<string, string>();

            var o = JToken.ReadFrom(reader);

            if (o["private"] != null && o["private"].Value<bool>() && o["workspaces"] != null)
            {
                if (o["workspaces"] is JArray)
                {
                    yarnWorkspaces = o["workspaces"].Values<string>().ToList();
                }
                else if (o["workspaces"] is JObject && o["workspaces"]["packages"] != null && o["workspaces"]["packages"] is JArray)
                {
                    yarnWorkspaces = o["workspaces"]["packages"].Values<string>().ToList();
                }
            }

            dependencies = PullDependenciesFromJToken(o, "dependencies");
            dependencies = dependencies.Concat(PullDependenciesFromJToken(o, "optionalDependencies")).ToDictionary(x => x.Key, x => x.Value);
            devDependencies = PullDependenciesFromJToken(o, "devDependencies");

            var returnedDependencies = AttachDevInformationToDependencies(dependencies, false);
            return returnedDependencies.Concat(AttachDevInformationToDependencies(devDependencies, true)).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.First().Value);
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

        private static IDictionary<string, string> PullDependenciesFromJToken(JToken jObject, string dependencyType)
        {
            IDictionary<string, JToken> dependencyJObject = new Dictionary<string, JToken>();
            if (jObject[dependencyType] != null)
            {
                dependencyJObject = (JObject)jObject[dependencyType];
            }

            return dependencyJObject.ToDictionary(x => x.Key, x => (string)x.Value);
        }

        private static bool IsPackageNameValid(string name)
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
    }
}
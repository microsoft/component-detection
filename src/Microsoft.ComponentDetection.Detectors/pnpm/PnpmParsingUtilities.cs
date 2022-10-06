using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using NuGet.Versioning;
using YamlDotNet.Serialization;

namespace Microsoft.ComponentDetection.Detectors.Pnpm
{
    public static class PnpmParsingUtilities
    {
        public static async Task<PnpmYaml> DeserializePnpmYamlFile(IComponentStream file)
        {
            var text = await new StreamReader(file.Stream).ReadToEndAsync();
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            return deserializer.Deserialize<PnpmYaml>(new StringReader(text));
        }

        public static DetectedComponent CreateDetectedComponentFromPnpmPath(string pnpmPackagePath)
        {
            var (parentName, parentVersion) = ExtractNameAndVersionFromPnpmPackagePath(pnpmPackagePath);
            return new DetectedComponent(new NpmComponent(parentName, parentVersion));
        }

        public static bool IsPnpmPackageDevDependency(Package pnpmPackage)
        {
            if (pnpmPackage == null)
            {
                throw new ArgumentNullException(nameof(pnpmPackage));
            }

            return string.Equals(bool.TrueString, pnpmPackage.dev, StringComparison.InvariantCultureIgnoreCase);
        }

        private static (string Name, string Version) ExtractNameAndVersionFromPnpmPackagePath(string pnpmPackagePath)
        {
            var pnpmComponentDefSections = pnpmPackagePath.Trim('/').Split('/');
            (var packageVersion, var indexVersionIsAt) = GetPackageVersion(pnpmComponentDefSections);
            if (indexVersionIsAt == -1)
            {
                // No version = not expected input
                return (null, null);
            }

            var normalizedPackageName = string.Join("/", pnpmComponentDefSections.Take(indexVersionIsAt).ToArray());
            return (normalizedPackageName, packageVersion);
        }

        private static (string PackageVersion, int VersionIndex) GetPackageVersion(string[] pnpmComponentDefSections)
        {
            var indexVersionIsAt = -1;
            var packageVersion = string.Empty;
            var lastIndex = pnpmComponentDefSections.Length - 1;

            // get version from packages with format /mute-stream/0.0.6
            if (SemanticVersion.TryParse(pnpmComponentDefSections[lastIndex], out var _))
            {
                return (pnpmComponentDefSections[lastIndex], lastIndex);
            }

            // get version from packages with format /@babel/helper-compilation-targets/7.10.4_@babel+core@7.10.5
            var lastComponentSplit = pnpmComponentDefSections[lastIndex].Split("_");
            if (SemanticVersion.TryParse(lastComponentSplit[0], out var _))
            {
                return (lastComponentSplit[0], lastIndex);
            }

            // get version from packages with format /sinon-chai/2.8.0/chai@3.5.0+sinon@1.17.7
            if (SemanticVersion.TryParse(pnpmComponentDefSections[lastIndex - 1], out var _))
            {
                return (pnpmComponentDefSections[lastIndex - 1], lastIndex - 1);
            }

            return (packageVersion, indexVersionIsAt);
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using System.IO;
using System.Linq;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using YamlDotNet.Serialization;

public static class PnpmParsingUtilities
{
    public static string DeserializePnpmYamlFileVersion(string fileContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<PnpmYamlVersion>(new StringReader(fileContent)).lockfileVersion;
    }

    public static bool IsPnpmPackageDevDependency(Package pnpmPackage)
    {
        if (pnpmPackage == null)
        {
            throw new ArgumentNullException(nameof(pnpmPackage));
        }

        return string.Equals(bool.TrueString, pnpmPackage.dev, StringComparison.InvariantCultureIgnoreCase);
    }

    public static PnpmYamlV5 DeserializePnpmYamlV5File(string fileContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<PnpmYamlV5>(new StringReader(fileContent));
    }

    public static PnpmYamlV6 DeserializePnpmYamlV6File(string fileContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<PnpmYamlV6>(new StringReader(fileContent));
    }

    /// <summary>
    /// Parse a pnpm path of the form "/package-name/version".
    /// </summary>
    /// <param name="pnpmPackagePath">a pnpm path of the form "/package-name/version".</param>
    /// <returns>Data parsed from path.</returns>
    public static DetectedComponent CreateDetectedComponentFromPnpmPathV5(string pnpmPackagePath)
    {
        var (parentName, parentVersion) = ExtractNameAndVersionFromPnpmPackagePathV5(pnpmPackagePath);
        return new DetectedComponent(new NpmComponent(parentName, parentVersion));
    }

    private static (string Name, string Version) ExtractNameAndVersionFromPnpmPackagePathV5(string pnpmPackagePath)
    {
        var pnpmComponentDefSections = pnpmPackagePath.Trim('/').Split('/');
        (var packageVersion, var indexVersionIsAt) = GetPackageVersionV5(pnpmComponentDefSections);
        if (indexVersionIsAt == -1)
        {
            // No version = not expected input
            return (null, null);
        }

        var normalizedPackageName = string.Join("/", pnpmComponentDefSections.Take(indexVersionIsAt).ToArray());
        return (normalizedPackageName, packageVersion);
    }

    /// <summary>
    /// Parse a pnpm dependency path.
    /// </summary>
    /// <param name="pnpmDependencyPath">A pnpm dependency path of the form "/@optional-scope/package-name@version(optional-ignored-data)(optional-ignored-data)".</param>
    /// <returns>Data parsed from path.</returns>
    public static DetectedComponent CreateDetectedComponentFromPnpmPathV6(string pnpmDependencyPath)
    {
        /*
         * The format is documented at https://github.com/pnpm/spec/blob/master/dependency-path.md.
         * At the writing it does not seem to reflect changes which were made in lock file format v6:
         * See https://github.com/pnpm/spec/issues/5.
         */

        // Strip parenthesized suffices from package. These hold peed dep related information that is unneeded here.
        // An example of a dependency path with these: /webpack-cli@4.10.0(webpack-bundle-analyzer@4.10.1)(webpack-dev-server@4.6.0)(webpack@5.89.0)
        var fullPackageNameAndVersion = pnpmDependencyPath.Split("(")[0];

        var packageNameParts = fullPackageNameAndVersion.Split("@");

        // If package name contains `@` this will reconstruct it:
        var fullPackageName = string.Join("@", packageNameParts[..^1]);

        // Version is section after last `@`.
        var packageVersion = packageNameParts[^1];

        // Check for leading `/` from pnpm.
        if (!fullPackageName.StartsWith("/"))
        {
            throw new FormatException("Found pnpm dependency path not starting with `/`. This case is currently unhandled.");
        }

        // Strip leading `/`.
        // It is unclear if real packages could have a name starting with `/`, so avoid `TrimStart` that just in case.
        var normalizedPackageName = fullPackageName[1..];

        return new DetectedComponent(new NpmComponent(normalizedPackageName, packageVersion));
    }

    /// <summary>
    /// Combine the information from a dependency edge in the dependency graph encoded in the ymal file into a full pnpm dependency path.
    /// </summary>
    /// <param name="dependencyName">The name of the dependency, as used as as the dictionary key in the yaml file when referring to the dependency.</param>
    /// <param name="dependencyVersion">The final resolved version of the package for this dependency edge.
    /// This includes details like which version of specific dependencies were specified as peer dependencies.
    /// In some edge cases, such as aliased packages, this version may be an absolute dependency path (starts with a slash) leaving the "dependencyName" unused.</param>
    /// <returns>A pnpm dependency path for the specified version of the named package.</returns>
    public static string ReconstructPnpmDependencyPathV6(string dependencyName, string dependencyVersion)
    {
        if (dependencyVersion.StartsWith("/"))
        {
            return dependencyVersion;
        }
        else
        {
            return $"/{dependencyName}@{dependencyVersion}";
        }
    }

    private static (string PackageVersion, int VersionIndex) GetPackageVersionV5(string[] pnpmComponentDefSections)
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

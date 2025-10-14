#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class PnpmV9ParsingUtilities<T> : PnpmParsingUtilitiesBase<T>
where T : PnpmYaml
{
    public override DetectedComponent CreateDetectedComponentFromPnpmPath(string pnpmPackagePath)
    {
        /*
         * The format is documented at https://github.com/pnpm/spec/blob/master/dependency-path.md.
         * At the writing it does not seem to reflect changes which were made in lock file format v9:
         * See https://github.com/pnpm/spec/issues/5.
         * In general, the spec sheet for the v9 lockfile is not published, so parsing of this lockfile was emperically determined.
         * see https://github.com/pnpm/spec/issues/6
         */

        // Strip parenthesized suffices from package. These hold peed dep related information that is unneeded here.
        // An example of a dependency path with these: /webpack-cli@4.10.0(webpack-bundle-analyzer@4.10.1)(webpack-dev-server@4.6.0)(webpack@5.89.0)
        (var fullPackageName, var packageVersion) = this.ExtractNameAndVersionFromPnpmPackagePath(pnpmPackagePath);

        return new DetectedComponent(new NpmComponent(fullPackageName, packageVersion));
    }

    public override (string FullPackageName, string PackageVersion) ExtractNameAndVersionFromPnpmPackagePath(string pnpmPackagePath)
    {
        /*
         * The format is documented at https://github.com/pnpm/spec/blob/master/dependency-path.md.
         * At the writing it does not seem to reflect changes which were made in lock file format v9:
         * See https://github.com/pnpm/spec/issues/5.
         * In general, the spec sheet for the v9 lockfile is not published, so parsing of this lockfile was emperically determined.
         * see https://github.com/pnpm/spec/issues/6
         */

        // Strip parenthesized suffices from package. These hold peed dep related information that is unneeded here.
        // An example of a dependency path with these: /webpack-cli@4.10.0(webpack-bundle-analyzer@4.10.1)(webpack-dev-server@4.6.0)(webpack@5.89.0)
        var fullPackageNameAndVersion = pnpmPackagePath.Split("(")[0];

        var packageNameParts = fullPackageNameAndVersion.Split("@");

        // If package name contains `@` this will reconstruct it:
        var fullPackageName = string.Join("@", packageNameParts[..^1]);

        // Version is section after last `@`.
        var packageVersion = packageNameParts[^1];

        return (fullPackageName, packageVersion);
    }

    /// <summary>
    /// Combine the information from a dependency edge in the dependency graph encoded in the ymal file into a full pnpm dependency path.
    /// </summary>
    /// <param name="dependencyName">The name of the dependency, as used as as the dictionary key in the yaml file when referring to the dependency.</param>
    /// <param name="dependencyVersion">The final resolved version of the package for this dependency edge.
    /// This includes details like which version of specific dependencies were specified as peer dependencies.
    /// In some edge cases, such as aliased packages, this version may be an absolute dependency path, but the leading slash has been removed.
    /// leaving the "dependencyName" unused, which is checked by whether there is an @ in the version. </param>
    /// <returns>A pnpm dependency path for the specified version of the named package.</returns>
    public override string ReconstructPnpmDependencyPath(string dependencyName, string dependencyVersion)
    {
        if (dependencyVersion.StartsWith('/') || dependencyVersion.Split("(")[0].Contains('@'))
        {
            return dependencyVersion;
        }
        else
        {
            return $"{dependencyName}@{dependencyVersion}";
        }
    }
}

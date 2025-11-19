#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class PnpmV6ParsingUtilities<T> : PnpmParsingUtilitiesBase<T>
where T : PnpmYaml
{
    public override DetectedComponent CreateDetectedComponentFromPnpmPath(string pnpmPackagePath)
    {
        /*
         * The format is documented at https://github.com/pnpm/spec/blob/master/dependency-path.md.
         * At the writing it does not seem to reflect changes which were made in lock file format v6:
         * See https://github.com/pnpm/spec/issues/5.
         */

        // Strip parenthesized suffices from package. These hold peed dep related information that is unneeded here.
        // An example of a dependency path with these: /webpack-cli@4.10.0(webpack-bundle-analyzer@4.10.1)(webpack-dev-server@4.6.0)(webpack@5.89.0)
        var (normalizedPackageName, packageVersion) = this.ExtractNameAndVersionFromPnpmPackagePath(pnpmPackagePath);
        return new DetectedComponent(new NpmComponent(normalizedPackageName, packageVersion));
    }

    public override (string FullPackageName, string PackageVersion) ExtractNameAndVersionFromPnpmPackagePath(string pnpmPackagePath)
    {
        var fullPackageNameAndVersion = pnpmPackagePath.Split("(")[0];

        var packageNameParts = fullPackageNameAndVersion.Split("@");

        // If package name contains `@` this will reconstruct it:
        var fullPackageName = string.Join("@", packageNameParts[..^1]);

        // Version is section after last `@`.
        var packageVersion = packageNameParts[^1];

        // Check for leading `/` from pnpm.
        if (!fullPackageName.StartsWith('/'))
        {
            throw new FormatException("Found pnpm dependency path not starting with `/`. This case is currently unhandled.");
        }

        // Strip leading `/`.
        // It is unclear if real packages could have a name starting with `/`, so avoid `TrimStart` that just in case.
        var normalizedPackageName = fullPackageName[1..];

        return (normalizedPackageName, packageVersion);
    }
}

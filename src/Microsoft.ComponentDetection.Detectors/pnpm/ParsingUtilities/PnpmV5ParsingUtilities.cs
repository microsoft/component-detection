#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System.Linq;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class PnpmV5ParsingUtilities<T> : PnpmParsingUtilitiesBase<T>
where T : PnpmYaml
{
    public override DetectedComponent CreateDetectedComponentFromPnpmPath(string pnpmPackagePath)
    {
        var (parentName, parentVersion) = this.ExtractNameAndVersionFromPnpmPackagePath(pnpmPackagePath);
        return new DetectedComponent(new NpmComponent(parentName, parentVersion));
    }

    public override (string FullPackageName, string PackageVersion) ExtractNameAndVersionFromPnpmPackagePath(string pnpmPackagePath)
    {
        var pnpmComponentDefSections = pnpmPackagePath.Trim('/').Split('/');
        (var packageVersion, var indexVersionIsAt) = this.GetPackageVersion(pnpmComponentDefSections);
        if (indexVersionIsAt == -1)
        {
            // No version = not expected input
            return (null, null);
        }

        var normalizedPackageName = string.Join("/", pnpmComponentDefSections.Take(indexVersionIsAt).ToArray());
        return (normalizedPackageName, packageVersion);
    }

    private (string PackageVersion, int VersionIndex) GetPackageVersion(string[] pnpmComponentDefSections)
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

namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Generic;
using global::NuGet.Frameworks;
using global::NuGet.Versioning;

/// <summary>
/// Represents a set of packages that are provided by a specific framework.
/// </summary>
/// <param name="framework">framework that this instance applies to.</param>
internal sealed partial class FrameworkPackages(NuGetFramework framework) : IEnumerable<KeyValuePair<string, NuGetVersion>>, IEnumerable
{
    static Dictionary<NuGetFramework, FrameworkPackages> _frameworkPackages;

    public FrameworkPackages(NuGetFramework framework, FrameworkPackages frameworkPackages)
        : this(frameworkPackages.Framework) => this.Packages = new(frameworkPackages.Packages);

    public NuGetFramework Framework { get; } = framework;

    // Adapted from https://github.com/dotnet/sdk/blob/c3a8f72c3a5491c693ff8e49e7406136a12c3040/src/Tasks/Common/ConflictResolution/PackageOverride.cs#L52-L68
    public Dictionary<string, NuGetVersion> Packages { get; } = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    private void Add(string id, string version)
    {
        this.Packages.Add(id, NuGetVersion.Parse(version));
    }

    private static IEnumerable<Tuple<string, NuGetVersion>> CreateOverriddenPackages(string overriddenPackagesString)
    {
        if (!string.IsNullOrEmpty(overriddenPackagesString))
        {
            overriddenPackagesString = overriddenPackagesString.Trim();
            var overriddenPackagesAndVersions = overriddenPackagesString.Split(new char[] { ';', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var overriddenPackagesAndVersion in overriddenPackagesAndVersions)
            {
                var trimmedOverriddenPackagesAndVersion = overriddenPackagesAndVersion.Trim();
                var separatorIndex = trimmedOverriddenPackagesAndVersion.IndexOf('|');
                if (separatorIndex != -1)
                {
                    var versionString = trimmedOverriddenPackagesAndVersion[(separatorIndex + 1)..];
                    var overriddenPackage = trimmedOverriddenPackagesAndVersion[..separatorIndex];
                    if (NuGetVersion.TryParse(versionString, out var version))
                    {
                        yield return Tuple.Create(overriddenPackage, version);
                    }
                }
            }
        }
    }

    IEnumerator<KeyValuePair<string, NuGetVersion>> IEnumerable<KeyValuePair<string, NuGetVersion>>.GetEnumerator() => this.Packages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

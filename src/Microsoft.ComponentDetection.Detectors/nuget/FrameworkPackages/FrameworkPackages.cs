namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using global::NuGet.Frameworks;
using global::NuGet.Versioning;

/// <summary>
/// Represents a set of packages that are provided by a specific framework.
/// At the moment this only represents the packages that are provided by the Microsoft.NETCore.App framework.
/// We could extend this to represent the packages provided by other frameworks like Microsoft.AspNetCore.App and Microsoft.WindowsDesktop.App.
/// </summary>
internal sealed partial class FrameworkPackages : IEnumerable<KeyValuePair<string, NuGetVersion>>, IEnumerable
{
    private static readonly Dictionary<NuGetFramework, FrameworkPackages> FrameworkPackagesByFramework = new();

    static FrameworkPackages()
    {
        FrameworkPackagesByFramework[NETStandard20] = NETStandard20Packages;
        FrameworkPackagesByFramework[NETStandard21] = NETStandard21Packages;
        FrameworkPackagesByFramework[NETCoreApp20] = NETCoreApp20Packages;
        FrameworkPackagesByFramework[NETCoreApp21] = NETCoreApp21Packages;
        FrameworkPackagesByFramework[NETCoreApp30] = NETCoreApp30Packages;
        FrameworkPackagesByFramework[NETCoreApp31] = NETCoreApp31Packages;
        FrameworkPackagesByFramework[NETCoreApp50] = NETCoreApp50Packages;
        FrameworkPackagesByFramework[NETCoreApp60] = NETCoreApp60Packages;
        FrameworkPackagesByFramework[NETCoreApp70] = NETCoreApp70Packages;
        FrameworkPackagesByFramework[NETCoreApp80] = NETCoreApp80Packages;
        FrameworkPackagesByFramework[NETCoreApp90] = NETCoreApp90Packages;
    }

    public FrameworkPackages(NuGetFramework framework) => this.Framework = framework;

    public FrameworkPackages(NuGetFramework framework, FrameworkPackages frameworkPackages)
        : this(frameworkPackages.Framework) => this.Packages = new(frameworkPackages.Packages);

    public NuGetFramework Framework { get; }

    // Adapted from https://github.com/dotnet/sdk/blob/c3a8f72c3a5491c693ff8e49e7406136a12c3040/src/Tasks/Common/ConflictResolution/PackageOverride.cs#L52-L68
    public Dictionary<string, NuGetVersion> Packages { get; } = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    public static FrameworkPackages GetFrameworkPackages(NuGetFramework framework)
    {
        if (FrameworkPackagesByFramework.TryGetValue(framework, out var frameworkPackages))
        {
            return frameworkPackages;
        }

        var frameworkPackagesFromPack = LoadFrameworkPackagesFromPack(framework);

        return FrameworkPackagesByFramework[framework] = frameworkPackagesFromPack ?? new FrameworkPackages(framework);
    }

    private static FrameworkPackages LoadFrameworkPackagesFromPack(NuGetFramework framework)
    {
        if (framework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            return null;
        }

        // packs location : %ProgramFiles%\dotnet\packs
        var packsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "packs", "Microsoft.NETCore.App.Ref");
        var packVersionPattern = $"{framework.Version.Major}.{framework.Version.Minor}.*";
        var packDirectories = Directory.GetDirectories(packsFolder, packVersionPattern);
        var packageOverridesFile = packDirectories
                                        .Select(d => (Overrides: Path.Combine(d, "data", "PackageOverrides.txt"), Version: ParseVersion(Path.GetFileName(d))))
                                        .Where(d => File.Exists(d.Overrides))
                                        .OrderByDescending(d => d.Version)
                                        .FirstOrDefault().Overrides;

        if (packageOverridesFile == null)
        {
            return null;
        }

        var frameworkPackages = new FrameworkPackages(framework);
        var packageOverrides = File.ReadAllLines(packageOverridesFile);

        foreach (var packageOverride in packageOverrides)
        {
            var packageOverrideParts = packageOverride.Trim().Split('|');

            if (packageOverrideParts.Length == 2)
            {
                var packageId = packageOverrideParts[0];
                var packageVersion = ParseVersion(packageOverrideParts[1]);

                frameworkPackages.Packages[packageId] = packageVersion;
            }
        }

        return frameworkPackages;

        static NuGetVersion ParseVersion(string versionString) => NuGetVersion.TryParse(versionString, out var version) ? version : null;
    }

    private void Add(string id, string version)
    {
        this.Packages.Add(id, NuGetVersion.Parse(version));
    }

    public bool IsAFrameworkComponent(string id, NuGetVersion version) => this.Packages.TryGetValue(id, out var frameworkPackageVersion) && frameworkPackageVersion >= version;

    IEnumerator<KeyValuePair<string, NuGetVersion>> IEnumerable<KeyValuePair<string, NuGetVersion>>.GetEnumerator() => this.Packages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

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
    private static readonly Dictionary<NuGetFramework, FrameworkPackages> FrameworkPackagesByFramework = [];

    static FrameworkPackages()
    {
        AddPackages(NETStandard20.Instance);
        AddPackages(NETStandard21.Instance);
        AddPackages(NETCoreApp20.Instance);
        AddPackages(NETCoreApp21.Instance);
        AddPackages(NETCoreApp22.Instance);
        AddPackages(NETCoreApp30.Instance);
        AddPackages(NETCoreApp31.Instance);
        AddPackages(NETCoreApp50.Instance);
        AddPackages(NETCoreApp60.Instance);
        AddPackages(NETCoreApp70.Instance);
        AddPackages(NETCoreApp80.Instance);
        AddPackages(NETCoreApp90.Instance);

        static void AddPackages(FrameworkPackages packages) => FrameworkPackagesByFramework[packages.Framework] = packages;
    }

    public FrameworkPackages(NuGetFramework framework) => this.Framework = framework;

    public FrameworkPackages(NuGetFramework framework, FrameworkPackages frameworkPackages)
        : this(framework) => this.Packages = new(frameworkPackages.Packages);

    public NuGetFramework Framework { get; }

    public Dictionary<string, NuGetVersion> Packages { get; } = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    public static FrameworkPackages GetFrameworkPackages(NuGetFramework framework)
    {
        if (FrameworkPackagesByFramework.TryGetValue(framework, out var frameworkPackages))
        {
            return frameworkPackages;
        }

        // if we didn't predefine the package overrides, load them from the targeting pack
        // we might just leave this out since in future frameworks we'll have this functionality built into NuGet.
        var frameworkPackagesFromPack = LoadFrameworkPackagesFromPack(framework);

        return FrameworkPackagesByFramework[framework] = frameworkPackagesFromPack ?? new FrameworkPackages(framework);
    }

    private static FrameworkPackages LoadFrameworkPackagesFromPack(NuGetFramework framework)
    {
        if (framework is null || framework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            return null;
        }

        // packs location : %ProgramFiles%\dotnet\packs
        var packsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "packs", "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(packsFolder))
        {
            return null;
        }

        var packVersionPattern = $"{framework.Version.Major}.{framework.Version.Minor}.*";
        var packDirectories = Directory.GetDirectories(packsFolder, packVersionPattern);
        var packageOverridesFile = packDirectories
                                        .Select(d => (Overrides: Path.Combine(d, "data", "PackageOverrides.txt"), Version: ParseVersion(Path.GetFileName(d))))
                                        .Where(d => File.Exists(d.Overrides))
                                        .OrderByDescending(d => d.Version)
                                        .FirstOrDefault().Overrides;

        if (packageOverridesFile == null)
        {
            // we should also try to grab them from the user's package folder - they'll be in one location or the other.
            return null;
        }

        // Adapted from https://github.com/dotnet/sdk/blob/c3a8f72c3a5491c693ff8e49e7406136a12c3040/src/Tasks/Common/ConflictResolution/PackageOverride.cs#L52-L68
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
        // intentionally redirect to indexer to allow for overwrite
        this.Packages[id] = NuGetVersion.Parse(version);
    }

    public bool IsAFrameworkComponent(string id, NuGetVersion version) => this.Packages.TryGetValue(id, out var frameworkPackageVersion) && frameworkPackageVersion >= version;

    IEnumerator<KeyValuePair<string, NuGetVersion>> IEnumerable<KeyValuePair<string, NuGetVersion>>.GetEnumerator() => this.Packages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
}

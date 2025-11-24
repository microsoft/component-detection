#nullable disable
namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using global::NuGet.Frameworks;
using global::NuGet.Packaging.Core;
using global::NuGet.ProjectModel;
using global::NuGet.Versioning;

/// <summary>
/// Represents a set of packages that are provided by a specific framework.
/// At the moment this only represents the packages that are provided by the Microsoft.NETCore.App framework.
/// We could extend this to represent the packages provided by other frameworks like Microsoft.AspNetCore.App and Microsoft.WindowsDesktop.App.
/// </summary>
internal sealed partial class FrameworkPackages : IEnumerable<KeyValuePair<string, NuGetVersion>>, IEnumerable
{
    private const string DefaultFrameworkKey = "";
    private static readonly ConcurrentDictionary<NuGetFramework, ConcurrentDictionary<string, FrameworkPackages>> FrameworkPackagesByFramework = [];

    static FrameworkPackages()
    {
        NETStandard20.Register();
        NETStandard21.Register();
        NET461.Register();
        NETCoreApp20.Register();
        NETCoreApp21.Register();
        NETCoreApp22.Register();
        NETCoreApp30.Register();
        NETCoreApp31.Register();
        NETCoreApp50.Register();
        NETCoreApp60.Register();
        NETCoreApp70.Register();
        NETCoreApp80.Register();
        NETCoreApp90.Register();
    }

    public FrameworkPackages(NuGetFramework framework, string frameworkName)
    {
        this.Framework = framework;
        this.FrameworkName = frameworkName;
    }

    public FrameworkPackages(NuGetFramework framework, string frameworkName, FrameworkPackages frameworkPackages)
        : this(framework, frameworkName) => this.Packages = new(frameworkPackages.Packages);

    public NuGetFramework Framework { get; }

    public string FrameworkName { get; }

    public Dictionary<string, NuGetVersion> Packages { get; } = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    private static string GetFrameworkKey(string frameworkName) =>
        frameworkName switch
        {
            FrameworkNames.NetStandardLibrary => DefaultFrameworkKey,
            FrameworkNames.NetCoreApp => DefaultFrameworkKey,
            _ => frameworkName,
        };

    internal static void Register(params FrameworkPackages[] toRegister)
    {
        foreach (var frameworkPackages in toRegister)
        {
            if (!FrameworkPackagesByFramework.TryGetValue(frameworkPackages.Framework, out var frameworkPackagesForVersion))
            {
                FrameworkPackagesByFramework[frameworkPackages.Framework] = frameworkPackagesForVersion = [];
            }

            var frameworkKey = GetFrameworkKey(frameworkPackages.FrameworkName);
            frameworkPackagesForVersion[frameworkKey] = frameworkPackages;
        }
    }

    public static FrameworkPackages[] GetFrameworkPackages(NuGetFramework framework, string[] frameworkReferences, LockFileTarget lockFileTarget)
    {
        var frameworkPackages = new List<FrameworkPackages>();

        if (frameworkReferences.Length == 0)
        {
            frameworkReferences = [DefaultFrameworkKey];
        }

        foreach (var frameworkReference in frameworkReferences)
        {
            var frameworkKey = GetFrameworkKey(frameworkReference);
            if (FrameworkPackagesByFramework.TryGetValue(framework, out var frameworkPackagesForVersion) &&
                frameworkPackagesForVersion.TryGetValue(frameworkKey, out var frameworkPackage))
            {
                frameworkPackages.Add(frameworkPackage);
            }
            else
            {
                // if we didn't predefine the package overrides, load them from the targeting pack
                // we might just leave this out since in future frameworks we'll have this functionality built into NuGet.Frameworks
                var frameworkPackagesFromPack = LoadFrameworkPackagesFromPack(framework, frameworkReference) ?? new FrameworkPackages(framework, frameworkReference);

                Register(frameworkPackagesFromPack);

                frameworkPackages.Add(frameworkPackagesFromPack);
            }
        }

        frameworkPackages.AddRange(GetLegacyFrameworkPackagesFromPlatformPackages(framework, lockFileTarget));

        return frameworkPackages.ToArray();
    }

    private static IEnumerable<FrameworkPackages> GetLegacyFrameworkPackagesFromPlatformPackages(NuGetFramework framework, LockFileTarget lockFileTarget)
    {
        if (framework.Framework == FrameworkConstants.FrameworkIdentifiers.NetCoreApp && framework.Version.Major < 3)
        {
            // For .NETCore 1.x (all frameworks) and 2.x (ASP.NET Core) the $(MicrosoftNETPlatformLibrary) property specified the framework package of the project.
            // This package and all its dependencies were excluded from copy to the output directory for framework deepndent apps.
            // See https://github.com/dotnet/sdk/blob/b3d6acae421815e90c74ddb631e426290348651c/src/Tasks/Microsoft.NET.Build.Tasks/ResolvePackageAssets.cs#L1882-L1898
            // We can't know from the assets file what the value of MicrosoftNETPlatformLibrary was, nor if an app was self-contained or not.
            // To avoid false positives, and since these frameworks are no longer supported, we'll just assume that all platform packages are framework,
            // and that the app is framework-dependent.
            // This is consistent with the old behavior of the old NuGet Detector.
            var lookup = lockFileTarget.Libraries.ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);
            string[] platformPackageNames = [
                FrameworkNames.NetCoreApp,           // Default platform package for .NET Core 1.x and 2.x    https://github.com/dotnet/sdk/blob/516dcf4a3bcf52ac3dce2452ea15ddd5cf057300/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L519
                FrameworkNames.AspNetCoreApp,        // ASP.NET platform package for .NET Core 2.x            https://github.com/dotnet/aspnetcore/blob/ac66280fe9024bdd686354e342fcdfb3409597f7/src/Microsoft.AspNetCore.App/build/netcoreapp2.1/Microsoft.AspNetCore.App.targets#L10
                "Microsoft.AspNetCore.All",          // Alternate ASP.NET platform package for .NET Core 2.x  https://github.com/dotnet/aspnetcore/blob/ac66280fe9024bdd686354e342fcdfb3409597f7/src/Microsoft.AspNetCore.All/build/netcoreapp2.1/Microsoft.AspNetCore.All.targets#L10

                // ASP.NET did not have platform package / shared framework for .NET Core 1.x - it was app-local only
            ];

            foreach (var platformPackageName in platformPackageNames)
            {
                if (lookup.TryGetValue(platformPackageName, out var platformLibrary))
                {
                    var frameworkPackagesFromPlatformPackage = new FrameworkPackages(framework, platformPackageName);

                    frameworkPackagesFromPlatformPackage.Packages.Add(platformLibrary.Name, platformLibrary.Version);

                    CollectDependencies(platformLibrary.Dependencies);

                    yield return frameworkPackagesFromPlatformPackage;

                    // recursively include dependencies, so long as they were not upgraded by some other reference
                    void CollectDependencies(IEnumerable<PackageDependency> dependencies)
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (lookup.TryGetValue(dependency.Id, out var library) &&
                                library.Version.Equals(dependency.VersionRange.MinVersion))
                            {
                                // if this is the first time we add the package, add its dependencies as well
                                if (frameworkPackagesFromPlatformPackage.Packages.TryAdd(library.Name, library.Version))
                                {
                                    CollectDependencies(library.Dependencies);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static FrameworkPackages LoadFrameworkPackagesFromPack(NuGetFramework framework, string frameworkName)
    {
        if (framework is null || framework.Framework != FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
        {
            return null;
        }

        var reducer = new FrameworkReducer();
        var frameworkKey = GetFrameworkKey(frameworkName);
        var candidateFrameworks = FrameworkPackagesByFramework.Where(pair => pair.Value.ContainsKey(frameworkKey)).Select(pair => pair.Key);
        var nearestFramework = reducer.GetNearest(framework, candidateFrameworks);

        var frameworkPackages = nearestFramework is null ?
            new FrameworkPackages(framework, frameworkName) :
            new FrameworkPackages(framework, frameworkName, FrameworkPackagesByFramework[nearestFramework][frameworkKey]);

        // packs location : %ProgramFiles%\dotnet\packs
        var packsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "packs", frameworkName + ".Ref");
        if (Directory.Exists(packsFolder))
        {
            var packVersionPattern = $"{framework.Version.Major}.{framework.Version.Minor}.*";
            var packDirectories = Directory.GetDirectories(packsFolder, packVersionPattern);
            var packageOverridesFile = packDirectories
                                            .Select(d => (Overrides: Path.Combine(d, "data", "PackageOverrides.txt"), Version: ParseVersion(Path.GetFileName(d))))
                                            .Where(d => File.Exists(d.Overrides))
                                            .OrderByDescending(d => d.Version)
                                            .FirstOrDefault().Overrides;

            if (packageOverridesFile is not null)
            {
                // Adapted from https://github.com/dotnet/sdk/blob/c3a8f72c3a5491c693ff8e49e7406136a12c3040/src/Tasks/Common/ConflictResolution/PackageOverride.cs#L52-L68
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
            }
        }

        return frameworkPackages;

        static NuGetVersion ParseVersion(string versionString) => NuGetVersion.TryParse(versionString, out var version) ? version : null;
    }

    private void Add(string id, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            this.Packages.Remove(id);
        }
        else
        {
            // intentionally redirect to indexer to allow for overwrite
            this.Packages[id] = NuGetVersion.Parse(version);
        }
    }

    public bool IsAFrameworkComponent(string id, NuGetVersion version) => this.Packages.TryGetValue(id, out var frameworkPackageVersion) && frameworkPackageVersion >= version;

    IEnumerator<KeyValuePair<string, NuGetVersion>> IEnumerable<KeyValuePair<string, NuGetVersion>>.GetEnumerator() => this.Packages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

    internal static class FrameworkNames
    {
        public const string AspNetCoreApp = "Microsoft.AspNetCore.App";
        public const string NetCoreApp = "Microsoft.NETCore.App";
        public const string NetStandardLibrary = "NETStandard.Library";
        public const string WindowsDesktopApp = "Microsoft.WindowsDesktop.App";
    }
}

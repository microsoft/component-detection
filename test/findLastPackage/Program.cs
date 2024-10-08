// See https://aka.ms/new-console-template for more information

using findLastPackage;
using Microsoft.DotNet.PackageValidation;
using NuGet.Frameworks;
using NuGet.Versioning;
using System.Diagnostics;
using System.Reflection;

(string path, string tfm)[] frameworkPacks = [
    ("C:\\Users\\ericstj\\.nuget\\packages\\netstandard.library\\2.0.3\\build\\netstandard2.0\\ref", "netstandard2.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\netstandard.library.ref\\2.1.0", "netstandard2.1"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app\\2.0.0", "netcoreapp2.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app\\2.1.0", "netcoreapp2.1"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\3.0.0", "netcoreapp3.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\3.1.0", "netcoreapp3.1"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\5.0.0", "net5.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\6.0.32", "net6.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\7.0.19", "net7.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\8.0.7", "net8.0"),
    ("C:\\Users\\ericstj\\.nuget\\packages\\microsoft.netcore.app.ref\\9.0.0-rc.2.24473.4", "net9.0")
    ];

// framework compat facades that shouldn't be compared against packages
HashSet<string> filesToIgnore = new(StringComparer.OrdinalIgnoreCase)
{
    "mscorlib",
    "Microsoft.VisualBasic",
    "System",
    "System.ComponentModel.DataAnnotations",
    "System.Configuration",
    "System.Core",
    "System.Data",
    "System.Drawing",
    "System.IO.Compression.FileSystem",
    "System.Net",
    "System.Numerics",
    "System.Runtime.Serialization",
    "System.Security",
    "System.ServiceProcess",
    "System.ServiceModel.Web",
    "System.Transactions",
    "System.Web",
    "System.Windows",
    "System.Xml",
    "System.Xml.Serialization",
    "System.Xml.Linq",
    "WindowsBase"
};

NuGetVersion? ParseVersion(string versionString) => NuGetVersion.TryParse(versionString, out var version) ? version : null;

var inboxPackages = new Dictionary<NuGetFramework, Dictionary<string, Version>>();
foreach (var frameworkPack in frameworkPacks)
{
    // calculate from assembly metadata
    var tfm = NuGetFramework.Parse(frameworkPack.tfm);
    var packages = ProcessFiles(frameworkPack.path, tfm).ToDictionary();

    // merge in overrides data
    var overridesFile = Path.Combine(frameworkPack.path, "data", "PackageOverrides.txt");
    if (File.Exists(overridesFile))
    {
        var packageOverrides = File.ReadAllLines(overridesFile);

        foreach (var packageOverride in packageOverrides)
        {
            var packageOverrideParts = packageOverride.Trim().Split('|');
            
            if (packageOverrideParts.Length == 2)
            {
                var packageId = packageOverrideParts[0];
                var packageVersion = Version.Parse(packageOverrideParts[1]);

                if (packages.TryGetValue(packageId, out var existingVersion))
                {
                    if (existingVersion < packageVersion)
                    {
                        Console.WriteLine($"{packageId} {tfm.GetShortFolderName()} -- Caclulated {existingVersion} < PackageOverrides {packageVersion}");
                    }
                    else if (existingVersion > packageVersion)
                    {
                        Console.WriteLine($"{packageId} {tfm.GetShortFolderName()} -- Caclulated {existingVersion} > PackageOverrides {packageVersion}");
                        continue;
                    }
                }

                packages[packageId] = packageVersion;
            }

        }
    }

    inboxPackages[tfm] = packages;
}

// reduce packages and emit
FrameworkReducer reducer = new();
foreach (var framework in inboxPackages.Keys)
{
    // create a copy to reduce, so we don't change the data used for reduction.
    var reducedPackages = new Dictionary<string, Version>(inboxPackages[framework]);

    // find the nearest framework not ourself, and remove any packages that it defines
    var nearest = reducer.GetNearest(framework, inboxPackages.Keys.Where(f => f != framework));

    if (nearest != null)
    {
        foreach (var package in inboxPackages[nearest])
        {
            if (reducedPackages.TryGetValue(package.Key, out var existingVersion))
            {
                if (existingVersion < package.Value)
                {
                    Console.WriteLine($"{package.Key} - Compatible framework {nearest} has higher version {package.Value} than {framework} - {existingVersion}");
                }
                else if (existingVersion == package.Value)
                {
                    // compatible framework has the same version referenced
                    reducedPackages.Remove(package.Key);
                }
                // else compatible framework has a lower version, keep it

            }
            else
            {
                Console.WriteLine($"{package.Key} - Compatible framework {nearest} has package but {framework} does not");
            }
        }
    }

    // write out our source file
    using StreamWriter fileWriter = new($"FrameworkPackages.{framework.GetShortFolderName()}.cs");

    // DotNetFrameworkName is something like ".NETStandard,Version=v2.0", convert to an identifier
    var tfmToken = framework.DotNetFrameworkName.Replace(",Version=v", "").Replace(".", "");
    var nearestTfmToken = nearest?.DotNetFrameworkName.Replace(",Version=v", "").Replace(".", "");

    fileWriter.WriteLine("namespace Microsoft.ComponentDetection.Detectors.NuGet;");
    fileWriter.WriteLine();
    fileWriter.WriteLine("using global::NuGet.Frameworks;");
    fileWriter.WriteLine();
    fileWriter.WriteLine("/// <summary>");
    fileWriter.WriteLine($"/// Framework packages for {framework.ToString()}.");
    fileWriter.WriteLine("/// </summary>");
    fileWriter.WriteLine("internal partial class FrameworkPackages");
    fileWriter.WriteLine("{");
    fileWriter.WriteLine($"    internal static class {tfmToken}");
    fileWriter.WriteLine("    {");
    fileWriter.WriteLine($"        internal static FrameworkPackages Instance {{ get; }} = new(NuGetFramework.Parse(\"{framework.GetShortFolderName()}\"){(nearestTfmToken != null ? $", {nearestTfmToken}.Instance" : "")})");
    fileWriter.WriteLine("        {");
    foreach(var package in reducedPackages.OrderBy(p => p.Key))
    {
        fileWriter.WriteLine($"        {{ \"{package.Key}\", \"{package.Value}\" }},");
    }
    fileWriter.WriteLine("        };");
    fileWriter.WriteLine("    }");
    fileWriter.WriteLine("}");
}

IEnumerable<(string packageId, Version version)> ProcessFiles(string referencePath, NuGetFramework tfm)
{
    if (!Directory.Exists(referencePath))
    {
        throw new Exception($"Directory doesn't exist {referencePath}");
    }

    if (!File.Exists(Path.Combine(referencePath, "System.Runtime.dll")))
    {
        referencePath = Path.Combine(referencePath, "ref", tfm.GetShortFolderName());

        if (!File.Exists(Path.Combine(referencePath, "System.Runtime.dll")))
            throw new Exception($"Couldn't find references in {referencePath}");
    }

    foreach (var libraryPath in Directory.EnumerateFiles(referencePath, "*.dll").Where(f => !filesToIgnore.Contains(Path.GetFileNameWithoutExtension(f))))
    { 
        var assemblyName = AssemblyName.GetAssemblyName(libraryPath);
        var packageId = assemblyName.Name;
        var assemblyVersion = assemblyName.Version;
        var assemblyFileVersion = Version.Parse(FileVersionInfo.GetVersionInfo(libraryPath).FileVersion);

        // For a library in a ref pack, look at all stable packages.  
        var stableVersions = NuGetUtilities.GetStableVersions2(packageId);
        // Starting with the latest download each.
        foreach (var stableVersion in stableVersions)
        {
            // Evaluate the package for the current framework.
            var packageContentVersions = NuGetUtilities.ResolvePackageAssetVersions(packageId, stableVersion, tfm);

            if (!packageContentVersions.Any())
            {
                continue;
            }

            bool packageWins = false;
            foreach (var packageContentVersion in packageContentVersions)
            {
                if (packageContentVersion.assemblyVersion > assemblyVersion)
                {
                    packageWins = true;
                    break;
                }

                if (packageContentVersion.assemblyVersion < assemblyVersion)
                {
                    break;
                }

                // equal assembly version

                if (packageContentVersion.fileVersion > assemblyFileVersion)
                {
                    packageWins = true;
                    break;
                }

                // package file version is equal to or less than -- package loses
            }

            // If the library wins, stop.  If it loses, then continue with the next newest package
            if (!packageWins)
            {
                yield return (packageId, stableVersion);
                break;
            }
        }
    }
}


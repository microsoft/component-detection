// See https://aka.ms/new-console-template for more information

using findLastPackage;
using NuGet.Frameworks;
using System.Diagnostics;
using System.Reflection;

string referenncesPath = args[0];
var tfm = NuGetFramework.Parse(args[1]);

foreach (var libraryPath in Directory.EnumerateFiles(referenncesPath, "*.dll"))
    ProcessFile(libraryPath);


void ProcessFile(string libraryPath)
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
                continue;
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
            Console.WriteLine($"{{ \"{packageId}\", \"{stableVersion}\"}}");
            break;
        }
    }
}



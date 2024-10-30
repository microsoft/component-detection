using Microsoft.DotNet.PackageValidation;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace findLastPackage
{
    internal static class NuGetUtilities
    {
        static NuGetUtilities()
        {
            Package.InitializeRuntimeGraph(Path.Combine(AppContext.BaseDirectory, "RuntimeIdentifierGraph.json"));
        }

        public static Version[] GetStableVersions(string packageId)
        {
            string allPackageVersionsUrl = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
            string versionsJson = string.Empty;
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var packageVersions = httpClient.GetFromJsonAsync<PackageVersions>(allPackageVersionsUrl).Result;

                    return packageVersions!.Versions.Where(s => !s.Contains('-')).Select(s => Version.Parse(s)).OrderDescending().ToArray();
                }
            }
            catch (Exception)
            {
                return [];
            }
        }

        public static Version[] GetStableVersions2(string packageId)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            PackageMetadataResource resource = repository.GetResourceAsync<PackageMetadataResource>().Result;

            IEnumerable<IPackageSearchMetadata> packages = resource.GetMetadataAsync(
                packageId,
                includePrerelease: false,
                includeUnlisted: false,
                cache,
                logger,
                cancellationToken).Result;

            return packages.Select(p => p.Identity.Version.As3PartVersion()).OrderDescending().ToArray();
        }

        private static Version As3PartVersion(this NuGetVersion nugetVersion) => new Version(nugetVersion.Major, nugetVersion.Minor, nugetVersion.Patch);

        public static IEnumerable<(string path, Version assemblyVersion, Version fileVersion)> ResolvePackageAssetVersions(string packageId, Version version, NuGetFramework framework)
        {
            string packageDownloadUrl = $"https://www.nuget.org/api/v2/package/{packageId}/{version}";

            using var packageStream = DownloadPackage(packageId, version);

            Package package;

            try
            {
                package = Package.Create(packageStream);
            }
            catch (InvalidDataException)
            {
                Console.WriteLine($"Error loading package {packageId}, {version}");
                yield break;
            }

            using (package)
            {
                // we will compare against runtime asset since this may be higher version
                // than compile - it's the one that's serviced when compile may be pinned
                var assets = package.FindBestRuntimeAssetForFramework(framework);

                // if we couldn't find RID-agnostic assets, try for a single RID.
                if (assets == null || assets.Count == 0)
                {
                    assets = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, "win");
                }

                if (assets == null || assets.Count == 0)
                {
                    assets = package.FindBestCompileAssetForFramework(framework);
                }

                if (assets != null)
                {
                    var matchingAssets = assets.Where(ca => Path.GetFileNameWithoutExtension(ca.Path).Equals(packageId, StringComparison.OrdinalIgnoreCase));

                    foreach (var asset in matchingAssets)
                    {
                        var entry = package.PackageReader.GetEntry(asset.Path);
                        using var assemblyStream = entry.Open();
                        using var seekableStream = new MemoryStream((int)entry.Length);
                        assemblyStream.CopyTo(seekableStream);
                        seekableStream.Position = 0;

                        var versions = AssemblyUtilities.GetVersions(seekableStream);

                        yield return (path: asset.Path, assemblyVersion: versions.assemblyVersion, fileVersion: versions.fileVersion);
                    }
                }
            }
        }

        private static Stream DownloadPackage(string packageId, Version version)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            FindPackageByIdResource resource = repository.GetResourceAsync<FindPackageByIdResource>().Result;

            NuGetVersion packageVersion = new NuGetVersion(version);
            MemoryStream packageStream = new MemoryStream();

            resource.CopyNupkgToStreamAsync(
                packageId,
                packageVersion,
                packageStream,
                cache,
                logger,
                cancellationToken).Wait();

            return packageStream;
        }

        private class PackageVersions
        {
            [JsonPropertyName("versions")]
            public string[] Versions { get; set; }
        }


    }
}

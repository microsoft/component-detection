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

            return packages.Select(p => p.Identity.Version.Version).OrderDescending().ToArray();
        }

        public static IEnumerable<(string path, Version assemblyVersion, Version fileVersion)> ResolvePackageAssetVersions(string packageId, Version version, NuGetFramework framework)
        {
            string packageDownloadUrl = $"https://www.nuget.org/api/v2/package/{packageId}/{version}";

            using (HttpClient httpClient = new HttpClient())
            {
                using var packageStream = httpClient.GetStreamAsync(packageDownloadUrl).Result;
                //using var seekableStream = new MemoryStream();
                //packageStream.CopyTo(seekableStream);

                using var package = Package.Create(packageStream);

                var assets = package.FindBestCompileAssetForFramework(framework);

                if (assets != null)
                {
                    var matchingCompileAssets = assets.Where(ca => Path.GetFileNameWithoutExtension(ca.Path).Equals(packageId, StringComparison.OrdinalIgnoreCase));

                    foreach (var asset in matchingCompileAssets)
                    {
                        var entry = package.PackageReader.GetEntry(asset.Path);
                        using var assemblyStream = entry.Open(); ;
                        using var seekableStream = new MemoryStream((int)entry.Length);
                        assemblyStream.CopyTo(seekableStream);
                        seekableStream.Position = 0;

                        var versions = AssemblyUtilities.GetVersions(seekableStream);

                        yield return (path: asset.Path, assemblyVersion: versions.assemblyVersion, fileVersion: versions.fileVersion);
                    }
                }
            }
        }

        private class PackageVersions
        {
            [JsonPropertyName("versions")]
            public string[] Versions { get; set; }
        }


    }
}

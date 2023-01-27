using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Polly;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Detectors.Tests")]

namespace Microsoft.ComponentDetection.Detectors.Pip;

public interface IPyPiClient
{
    Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(string name, string version, PythonProjectRelease release);

    Task<SortedDictionary<string, IList<PythonProjectRelease>>> GetReleasesAsync(PipDependencySpecification spec);
}

[Export(typeof(IPyPiClient))]
public class PyPiClient : IPyPiClient
{
    // Values used for cache creation
    private const long CACHEINTERVALSECONDS = 60;

    private const long DEFAULTCACHEENTRIES = 4096;

    // max number of retries allowed, to cap the total delay period
    private const long MAXRETRIES = 15;

    private static readonly HttpClientHandler HttpClientHandler = new() { CheckCertificateRevocationList = true };

    // time to wait before retrying a failed call to pypi.org
    private static readonly TimeSpan RETRYDELAY = TimeSpan.FromSeconds(1);

    // Keep telemetry on how the cache is being used for future refinements
    private readonly PypiCacheTelemetryRecord cacheTelemetry;

    private bool checkedMaxEntriesVariable = false;

    // retries used so far for calls to pypi.org
    private long retries = 0;

    /// <summary>
    /// A thread safe cache implementation which contains a mapping of URI -> HttpResponseMessage
    /// and has a limited number of entries which will expire after the cache fills or a specified interval.
    /// </summary>
    private MemoryCache cachedResponses = new(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });

    public PyPiClient() => this.cacheTelemetry = new PypiCacheTelemetryRecord()
    {
        NumCacheHits = 0,
        FinalCacheSize = 0,
    };

    ~PyPiClient()
    {
        this.cacheTelemetry.FinalCacheSize = this.cachedResponses.Count;
        this.cacheTelemetry.Dispose();
    }

    public static HttpClient HttpClient { get; internal set; } = new HttpClient(HttpClientHandler);

    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IEnvironmentVariableService EnvironmentVariableService { get; set; }

    public async Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(string name, string version, PythonProjectRelease release)
    {
        var dependencies = new List<PipDependencySpecification>();

        var uri = release.Url.ToString();
        var response = await this.GetAndCachePyPiResponseAsync(uri);

        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogWarning($"Http GET at {release.Url} failed with status code {response.StatusCode}");
            return dependencies;
        }

        var package = new ZipArchive(await response.Content.ReadAsStreamAsync());

        var entry = package.GetEntry($"{name.Replace('-', '_')}-{version}.dist-info/METADATA");

        // If there is no metadata file, the package doesn't have any declared dependencies
        if (entry == null)
        {
            return dependencies;
        }

        var content = new List<string>();
        using (var stream = entry.Open())
        {
            using var streamReader = new StreamReader(stream);

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();

                if (PipDependencySpecification.RequiresDistRegex.IsMatch(line))
                {
                    content.Add(line);
                }
            }
        }

        // Pull the packages that aren't conditional based on "extras"
        // Right now we just want to resolve the graph as most comsumers will
        // experience it
        foreach (var deps in content.Where(x => !x.Contains("extra ==")))
        {
            dependencies.Add(new PipDependencySpecification(deps, true));
        }

        return dependencies;
    }

    public async Task<SortedDictionary<string, IList<PythonProjectRelease>>> GetReleasesAsync(PipDependencySpecification spec)
    {
        var requestUri = $"https://pypi.org/pypi/{spec.Name}/json";

        var request = await Policy
            .HandleResult<HttpResponseMessage>(message =>
            {
                // stop retrying if MAXRETRIES was hit!
                if (message == null)
                {
                    return false;
                }

                var statusCode = (int)message.StatusCode;

                // only retry if server doesn't classify the call as a client error!
                var isRetryable = statusCode < 400 || statusCode > 499;
                return !message.IsSuccessStatusCode && isRetryable;
            })
            .WaitAndRetryAsync(1, i => RETRYDELAY, (result, timeSpan, retryCount, context) =>
            {
                using var r = new PypiRetryTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray(), StatusCode = result.Result.StatusCode };

                this.Logger.LogWarning($"Received {(int)result.Result.StatusCode} {result.Result.ReasonPhrase} from {requestUri}. Waiting {timeSpan} before retry attempt {retryCount}");

                Interlocked.Increment(ref this.retries);
            })
            .ExecuteAsync(() =>
            {
                if (Interlocked.Read(ref this.retries) >= MAXRETRIES)
                {
                    return Task.FromResult<HttpResponseMessage>(null);
                }

                return this.GetAndCachePyPiResponseAsync(requestUri);
            });

        if (request == null)
        {
            using var r = new PypiMaxRetriesReachedTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray() };

            this.Logger.LogWarning($"Call to pypi.org failed, but no more retries allowed!");

            return new SortedDictionary<string, IList<PythonProjectRelease>>();
        }

        if (!request.IsSuccessStatusCode)
        {
            using var r = new PypiFailureTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray(), StatusCode = request.StatusCode };

            this.Logger.LogWarning($"Received {(int)request.StatusCode} {request.ReasonPhrase} from {requestUri}");

            return new SortedDictionary<string, IList<PythonProjectRelease>>();
        }

        var response = await request.Content.ReadAsStringAsync();
        var project = JsonConvert.DeserializeObject<PythonProject>(response);
        var versions = new SortedDictionary<string, IList<PythonProjectRelease>>(new PythonVersionComparer());

        foreach (var release in project.Releases)
        {
            try
            {
                var parsedVersion = new PythonVersion(release.Key);
                if (release.Value != null && release.Value.Count > 0 &&
                    parsedVersion.Valid && parsedVersion.IsReleasedPackage &&
                    PythonVersionUtilities.VersionValidForSpec(release.Key, spec.DependencySpecifiers))
                {
                    versions.Add(release.Key, release.Value);
                }
            }
            catch (ArgumentException ae)
            {
                this.Logger.LogError($"Component {release.Key} : {JsonConvert.SerializeObject(release.Value)} could not be added to the sorted list of pip components for spec={spec.Name}. Usually this happens with unexpected PyPi version formats (e.g. prerelease/dev versions). Error details follow:");
                this.Logger.LogException(ae, true);
                continue;
            }
        }

        return versions;
    }

    /// <summary>
    /// Returns a cached response if it exists, otherwise returns the response from PyPi REST call.
    /// The response from PyPi is automatically added to the cache.
    /// </summary>
    /// <param name="uri">The REST Uri to call.</param>
    /// <returns>The cached response or a new result from PyPi.</returns>
    private async Task<HttpResponseMessage> GetAndCachePyPiResponseAsync(string uri)
    {
        if (!this.checkedMaxEntriesVariable)
        {
            this.InitializeNonDefaultMemoryCache();
        }

        if (this.cachedResponses.TryGetValue(uri, out HttpResponseMessage result))
        {
            this.cacheTelemetry.NumCacheHits++;
            this.Logger.LogVerbose("Retrieved cached Python data from " + uri);
            return result;
        }

        this.Logger.LogInfo("Getting Python data from " + uri);
        var response = await HttpClient.GetAsync(uri);

        // The `first - wins` response accepted into the cache. This might be different from the input if another caller wins the race.
        return await this.cachedResponses.GetOrCreateAsync(uri, cacheEntry =>
        {
            cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(CACHEINTERVALSECONDS); // This entry will expire after CACHEINTERVALSECONDS seconds from last use
            cacheEntry.Size = 1; // Specify a size of 1 so a set number of entries can always be in the cache
            return Task.FromResult(response);
        });
    }

    /// <summary>
    /// On the initial caching attempt, see if the user specified an override for
    /// PyPiMaxCacheEntries and recreate the cache if needed.
    /// </summary>
    private void InitializeNonDefaultMemoryCache()
    {
        var maxEntriesVariable = this.EnvironmentVariableService.GetEnvironmentVariable("PyPiMaxCacheEntries");
        if (!string.IsNullOrEmpty(maxEntriesVariable) && long.TryParse(maxEntriesVariable, out var maxEntries))
        {
            this.Logger.LogInfo($"Setting IPyPiClient max cache entries to {maxEntries}");
            this.cachedResponses = new MemoryCache(new MemoryCacheOptions { SizeLimit = maxEntries });
        }

        this.checkedMaxEntriesVariable = true;
    }
}

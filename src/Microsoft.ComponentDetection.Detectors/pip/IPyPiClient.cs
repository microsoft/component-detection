#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Detectors.Tests")]

namespace Microsoft.ComponentDetection.Detectors.Pip;

public interface IPyPiClient
{
    Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(string name, string version, PythonProjectRelease release);

    Task<PythonProject> GetProjectAsync(PipDependencySpecification spec);
}

public sealed class PyPiClient : IPyPiClient, IDisposable
{
    // Values used for cache creation
    private const long CACHEINTERVALSECONDS = 180;

    private const long DEFAULTCACHEENTRIES = 4096;

    // max number of retries allowed, to cap the total delay period
    private const long MAXRETRIES = 15;

    private static readonly HttpClientHandler HttpClientHandler = new HttpClientHandler() { CheckCertificateRevocationList = true };

    // time to wait before retrying a failed call to pypi.org
    private static readonly TimeSpan RETRYDELAY = TimeSpan.FromSeconds(1);

    private static readonly ProductInfoHeaderValue ProductValue = new(
        "ComponentDetection",
        Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    private static readonly ProductInfoHeaderValue CommentValue = new("(+https://github.com/microsoft/component-detection)");

    // Keep telemetry on how the cache is being used for future refinements
    private readonly PypiCacheTelemetryRecord cacheTelemetry;

    private readonly IEnvironmentVariableService environmentVariableService;
    private readonly ILogger<PyPiClient> logger;

    private bool checkedMaxEntriesVariable;

    // retries used so far for calls to pypi.org
    private long retries;

    /// <summary>
    /// A thread safe cache implementation which contains a mapping of URI -> HttpResponseMessage
    /// and has a limited number of entries which will expire after the cache fills or a specified interval.
    /// </summary>
    private MemoryCache cachedResponses = new MemoryCache(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });

    public PyPiClient() => this.cacheTelemetry = new PypiCacheTelemetryRecord()
    {
        NumCacheHits = 0,
        FinalCacheSize = 0,
    };

    public PyPiClient(IEnvironmentVariableService environmentVariableService, ILogger<PyPiClient> logger)
    {
        this.environmentVariableService = environmentVariableService;
        this.cacheTelemetry = new PypiCacheTelemetryRecord
        {
            NumCacheHits = 0,
            FinalCacheSize = 0,
        };
        this.logger = logger;
    }

    public static HttpClient HttpClient { get; internal set; } = new HttpClient(HttpClientHandler);

    public async Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(string name, string version, PythonProjectRelease release)
    {
        var dependencies = new List<PipDependencySpecification>();

        var uri = release.Url;
        var response = await this.GetAndCachePyPiResponseAsync(uri);

        if (!response.IsSuccessStatusCode)
        {
            this.logger.LogWarning("Http GET at {ReleaseUrl} failed with status code {ResponseStatusCode}", release.Url, response.StatusCode);
            return dependencies;
        }

        var package = new ZipArchive(await response.Content.ReadAsStreamAsync());

        var entryName = $"{name.Replace('-', '_')}-{version}.dist-info/METADATA";

        // first try case insensitive dicitonary lookup O(1), then attempt case-insensitive match O(entries)
        var entry = package.GetEntry(entryName)
            ?? package.Entries.FirstOrDefault(x => string.Equals(x.FullName, entryName, StringComparison.OrdinalIgnoreCase));

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

    public async Task<PythonProject> GetProjectAsync(PipDependencySpecification spec)
    {
        var requestUri = new Uri($"https://pypi.org/pypi/{spec.Name}/json");

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

                this.logger.LogWarning(
                    "Received status:{StatusCode} with reason:{ReasonPhrase} from {RequestUri}. Waiting {TimeSpan} before retry attempt {RetryCount}",
                    result.Result.StatusCode,
                    result.Result.ReasonPhrase,
                    requestUri,
                    timeSpan,
                    retryCount);

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

            this.logger.LogWarning($"Call to pypi.org failed, but no more retries allowed!");

            return new PythonProject();
        }

        if (!request.IsSuccessStatusCode)
        {
            using var r = new PypiFailureTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray(), StatusCode = request.StatusCode };

            this.logger.LogWarning("Received status:{StatusCode} with reason:{ReasonPhrase} from {RequestUri}", request.StatusCode, request.ReasonPhrase, requestUri);

            return new PythonProject();
        }

        var response = await request.Content.ReadAsStringAsync();
        var project = JsonConvert.DeserializeObject<PythonProject>(response);
        var versions = new PythonProject
        {
            Info = project.Info,
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>(new PythonVersionComparer()),
        };

        foreach (var release in project.Releases)
        {
            try
            {
                var parsedVersion = PythonVersion.Create(release.Key);
                if (release.Value != null && release.Value.Count > 0 &&
                    parsedVersion.Valid &&
                    PythonVersionUtilities.VersionValidForSpec(release.Key, spec.DependencySpecifiers))
                {
                    versions.Releases[release.Key] = release.Value;
                }
            }
            catch (ArgumentException ae)
            {
                this.logger.LogError(
                    ae,
                    "Component {ReleaseKey} : {ReleaseValue} could not be added to the sorted list of pip components for spec={SpecName}. Usually this happens with unexpected PyPi version formats (e.g. prerelease/dev versions).",
                    release.Key,
                    JsonConvert.SerializeObject(release.Value),
                    spec.Name);
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
    private async Task<HttpResponseMessage> GetAndCachePyPiResponseAsync(Uri uri)
    {
        if (!this.checkedMaxEntriesVariable)
        {
            this.InitializeNonDefaultMemoryCache();
        }

        if (this.cachedResponses.TryGetValue(uri, out HttpResponseMessage result))
        {
            this.cacheTelemetry.NumCacheHits++;
            this.logger.LogDebug("Retrieved cached Python data from {Uri}", uri);
            return result;
        }

        this.logger.LogInformation("Getting Python data from {Uri}", uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(ProductValue);
        request.Headers.UserAgent.Add(CommentValue);
        var response = await HttpClient.SendAsync(request);

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
        var maxEntriesVariable = this.environmentVariableService.GetEnvironmentVariable("PyPiMaxCacheEntries");
        if (!string.IsNullOrEmpty(maxEntriesVariable) && long.TryParse(maxEntriesVariable, out var maxEntries))
        {
            this.logger.LogInformation("Setting IPyPiClient max cache entries to {MaxEntries}", maxEntries);
            this.cachedResponses = new MemoryCache(new MemoryCacheOptions { SizeLimit = maxEntries });
        }

        this.checkedMaxEntriesVariable = true;
    }

    public void Dispose()
    {
        this.cacheTelemetry.FinalCacheSize = this.cachedResponses.Count;
        this.cacheTelemetry.Dispose();
        this.cachedResponses.Dispose();
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;

public sealed class SimplePyPiClient : ISimplePyPiClient, IDisposable
{
    // Values used for cache creation
    private const long CACHEINTERVALSECONDS = 180;
    private const long DEFAULTCACHEENTRIES = 4096;

    // max number of retries allowed, to cap the total delay period
    public const long MAXRETRIES = 15;

    private static readonly ProductInfoHeaderValue ProductValue = new(
    "ComponentDetection",
    Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    private static readonly ProductInfoHeaderValue CommentValue = new("(+https://github.com/microsoft/component-detection)");

    // time to wait before retrying a failed call to pypi.org
    private static readonly TimeSpan RETRYDELAY = TimeSpan.FromSeconds(1);

    private static readonly HttpClientHandler HttpClientHandler = new HttpClientHandler() { CheckCertificateRevocationList = true };

    private readonly IEnvironmentVariableService environmentVariableService;
    private readonly ILogger<SimplePyPiClient> logger;

    // Keep telemetry on how the cache is being used for future refinements
    private readonly SimplePypiCacheTelemetryRecord cacheTelemetry = new SimplePypiCacheTelemetryRecord();

    /// <summary>
    /// A thread safe cache implementation which contains a mapping of URI -> SimpleProject for simplepypi api projects
    /// and has a limited number of entries which will expire after the cache fills or a specified interval.
    /// </summary>
    private MemoryCache cachedSimplePyPiProjects = new MemoryCache(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });

    /// <summary>
    /// A thread safe cache implementation which contains a mapping of URI -> Stream for project wheel files
    /// and has a limited number of entries which will expire after the cache fills or a specified interval.
    /// </summary>
    private MemoryCache cachedProjectWheelFiles = new MemoryCache(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });

    private bool checkedMaxEntriesVariable;

    // retries used so far for calls to pypi.org
    private long retries;

    public SimplePyPiClient(IEnvironmentVariableService environmentVariableService, ILogger<SimplePyPiClient> logger)
    {
        this.environmentVariableService = environmentVariableService;
        this.logger = logger;
    }

    public static HttpClient HttpClient { get; internal set; } = new HttpClient(HttpClientHandler);

    /// <inheritdoc />
    public async Task<SimplePypiProject> GetSimplePypiProjectAsync(PipDependencySpecification spec)
    {
        var requestUri = new Uri($"https://pypi.org/simple/{spec.Name}");

        var project = await this.GetAndCacheSimpleProjectAsync(requestUri, spec);

        return project;
    }

    /// <inheritdoc />
    public async Task<Stream> FetchPackageFileStreamAsync(Uri releaseUrl)
    {
        var projectStream = await this.GetAndCacheProjectFileAsync(releaseUrl);

        return projectStream;
    }

    /// <summary>
    /// Returns a cached response if it exists, otherwise returns the response from PyPi REST call.
    /// The response from PyPi is automatically added to the cache.
    /// </summary>
    /// <param name="uri">The REST Uri to call.</param>
    /// <returns>The cached project file or a new result from Simple PyPi.</returns>
    private async Task<Stream> GetAndCacheProjectFileAsync(Uri uri)
    {
        if (!this.checkedMaxEntriesVariable)
        {
            this.cachedProjectWheelFiles = this.InitializeNonDefaultMemoryCache(this.cachedProjectWheelFiles);
        }

        if (this.cachedProjectWheelFiles.TryGetValue(uri, out Stream result))
        {
            this.cacheTelemetry.NumProjectFileCacheHits++;
            this.logger.LogDebug("Retrieved cached Python data from {Uri}", uri);
            return result;
        }

        var response = await this.GetPypiResponseAsync(uri);

        if (!response.IsSuccessStatusCode)
        {
            this.logger.LogWarning("Http GET at {ReleaseUrl} failed with status code {ResponseStatusCode}", uri, response.StatusCode);
            return new MemoryStream();
        }

        var responseContent = await response.Content.ReadAsStreamAsync();

        // The `first - wins` response accepted into the cache. This might be different from the input if another caller wins the race.
        return await this.cachedProjectWheelFiles.GetOrCreateAsync(uri, cacheEntry =>
        {
            cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(CACHEINTERVALSECONDS); // This entry will expire after CACHEINTERVALSECONDS seconds from last use
            cacheEntry.Size = 1; // Specify a size of 1 so a set number of entries can always be in the cache
            return Task.FromResult(responseContent);
        });
    }

    /// <summary>
    /// Returns a cached response if it exists, otherwise returns the response from PyPi REST call.
    /// The response from PyPi is automatically added to the cache.
    /// </summary>
    /// <param name="uri">The REST Uri to call.</param>
    /// <param name="spec">The PipDependencySpecification for the project. </param>
    /// <returns>The cached deserialized json object or a new result from Simple PyPi.</returns>
    private async Task<SimplePypiProject> GetAndCacheSimpleProjectAsync(Uri uri, PipDependencySpecification spec)
    {
        var pythonProject = new SimplePypiProject();
        if (!this.checkedMaxEntriesVariable)
        {
            this.cachedSimplePyPiProjects = this.InitializeNonDefaultMemoryCache(this.cachedSimplePyPiProjects);
        }

        if (this.cachedSimplePyPiProjects.TryGetValue(uri, out SimplePypiProject result))
        {
            this.cacheTelemetry.NumSimpleProjectCacheHits++;
            this.logger.LogDebug("Retrieved cached Python data from {Uri}", uri);
            return result;
        }

        var response = await this.RetryPypiRequestAsync(uri, spec);

        var responseContent = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(responseContent))
        {
            return pythonProject;
        }

        try
        {
            pythonProject = JsonSerializer.Deserialize<SimplePypiProject>(responseContent);
        }
        catch (JsonException e)
        {
            this.logger.LogError(
                    e,
                    "Unable to deserialize simple pypi project. This is possibly because the server responded with an unexpected content type. Spec Name = {SpecName}",
                    spec.Name);
            return new SimplePypiProject();
        }

        // The `first - wins` response accepted into the cache. This might be different from the input if another caller wins the race.
        return await this.cachedSimplePyPiProjects.GetOrCreateAsync(uri, cacheEntry =>
        {
            cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(CACHEINTERVALSECONDS); // This entry will expire after CACHEINTERVALSECONDS seconds from last use
            cacheEntry.Size = 1; // Specify a size of 1 so a set number of entries can always be in the cache
            return Task.FromResult(pythonProject);
        });
    }

    /// <summary>
    /// On the initial caching attempt, see if the user specified an override for
    /// PyPiMaxCacheEntries and recreate the cache if needed.
    /// </summary>
    private MemoryCache InitializeNonDefaultMemoryCache(MemoryCache cache)
    {
        var maxEntriesVariable = this.environmentVariableService.GetEnvironmentVariable("PyPiMaxCacheEntries");
        if (!string.IsNullOrEmpty(maxEntriesVariable) && long.TryParse(maxEntriesVariable, out var maxEntries))
        {
            this.logger.LogInformation("Setting ISimplePyPiClient max cache entries to {MaxEntries}", maxEntries);
            cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = maxEntries });
        }

        this.checkedMaxEntriesVariable = true;
        return cache;
    }

    /// <summary>
    /// Retries the request to PyPi if the response is not successful.
    /// </summary>
    /// <param name="uri"> uri of the request.</param>
    /// <param name="spec"> The pip dependency specification. </param>
    /// <returns> Returns the HttpResponseMessage. </returns>
    private async Task<HttpResponseMessage> RetryPypiRequestAsync(Uri uri, PipDependencySpecification spec)
    {
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
               var isRetryable = statusCode != 300 && statusCode != 406 && (statusCode < 400 || statusCode > 499);
               return !message.IsSuccessStatusCode && isRetryable;
           })
           .WaitAndRetryAsync((int)MAXRETRIES - 1, i => RETRYDELAY, (result, timeSpan, retryCount, context) =>
           {
               using var r = new PypiRetryTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray(), StatusCode = result.Result.StatusCode };
               this.logger.LogWarning(
                   "Received {StatusCode} {ReasonPhrase} from {RequestUri}. Waiting {TimeSpan} before retry attempt {RetryCount}",
                   result.Result.StatusCode,
                   result.Result.ReasonPhrase,
                   uri,
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

               return this.GetPypiResponseAsync(uri);
           });
        if (request == null)
        {
            using var r = new PypiMaxRetriesReachedTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray() };

            this.logger.LogWarning($"Call to simple pypi api failed, but no more retries allowed!");

            return new HttpResponseMessage();
        }

        if (!request.IsSuccessStatusCode)
        {
            using var r = new PypiFailureTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray(), StatusCode = request.StatusCode };

            this.logger.LogWarning("Received {StatusCode} {ReasonPhrase} from {RequestUri}", request.StatusCode, request.ReasonPhrase, uri);

            return new HttpResponseMessage();
        }

        return request;
    }

    /// <summary>
    /// Sends a request to pypi.
    /// </summary>
    /// <param name="uri">The uri of the request. </param>
    /// <returns> Returns the httpresponsemessage. </returns>
    private async Task<HttpResponseMessage> GetPypiResponseAsync(Uri uri)
    {
        this.logger.LogInformation("Getting Python data from {Uri}", uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(ProductValue);
        request.Headers.UserAgent.Add(CommentValue);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pypi.simple.v1+json"));
        var response = await HttpClient.SendAsync(request);
        return response;
    }

    public void Dispose()
    {
        this.cacheTelemetry.FinalSimpleProjectCacheSize = this.cachedSimplePyPiProjects.Count;
        this.cacheTelemetry.FinalProjectFileCacheSize = this.cachedProjectWheelFiles.Count;
        this.cacheTelemetry.Dispose();
        this.cachedProjectWheelFiles.Dispose();
        this.cachedSimplePyPiProjects.Dispose();
        HttpClient.Dispose();
    }
}

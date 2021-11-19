using System;
using System.Collections.Concurrent;
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
using Newtonsoft.Json;
using Polly;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Detectors.Tests")]

namespace Microsoft.ComponentDetection.Detectors.Pip
{
    public interface IPyPiClient
    {
        Task<IList<PipDependencySpecification>> FetchPackageDependencies(string name, string version, PythonProjectRelease release);

        Task<SortedDictionary<string, IList<PythonProjectRelease>>> GetReleases(PipDependencySpecification spec);
    }

    [Export(typeof(IPyPiClient))]
    public class PyPiClient : IPyPiClient
    {
        [Import]
        public ILogger Logger { get; set; }

        private static HttpClientHandler httpClientHandler = new HttpClientHandler() { CheckCertificateRevocationList = true };

        internal static HttpClient HttpClient = new HttpClient(httpClientHandler);

        // time to wait before retrying a failed call to pypi.org
        private static readonly TimeSpan RETRYDELAY = TimeSpan.FromSeconds(1);

        // max number of retries allowed, to cap the total delay period
        private const long MAXRETRIES = 15;

        // retries used so far for calls to pypi.org
        private long retries = 0;

        /// <summary>
        /// This cache is used mostly for consistency, to create a unified view of Pypi response.
        /// </summary>
        private readonly ConcurrentDictionary<string, Task<HttpResponseMessage>> cachedResponses = new ConcurrentDictionary<string, Task<HttpResponseMessage>>();

        /// <summary>
        /// Returns a cached response if it exists, otherwise returns the response from Pypi REST call.
        /// The response from Pypi is not automatically added to the cache, to allow caller to make that decision.
        /// </summary>
        /// <param name="uri">The REST Uri to call.</param>
        /// <returns>The cached response or a new result from Pypi.</returns>
        private async Task<HttpResponseMessage> GetPypiResponse(string uri)
        {
            if (cachedResponses.TryGetValue(uri, out var value))
            {
                return await value;
            }

            Logger.LogInfo("Getting Python data from " + uri);
            return await HttpClient.GetAsync(uri);
        }

        /// <summary>
        /// Used to update the consistency cache, decision has to be made by the caller to allow for retries!.
        /// </summary>
        /// <param name="uri">The REST Uri to call.</param>
        /// <param name="message">The proposed response by the caller to store for this Uri.</param>
        /// <returns>The `first-wins` response accepted into the cache.
        /// This might be different from the input if another caller wins the race!.</returns>
        private async Task<HttpResponseMessage> CachePypiResponse(string uri, HttpResponseMessage message)
        {
            if (!cachedResponses.TryAdd(uri, Task.FromResult(message)))
            {
                return await cachedResponses[uri];
            }

            return message;
        }

        public async Task<IList<PipDependencySpecification>> FetchPackageDependencies(string name, string version, PythonProjectRelease release)
        {
            var dependencies = new List<PipDependencySpecification>();

            var uri = release.Url.ToString();
            var response = await GetPypiResponse(uri);

            response = await CachePypiResponse(uri, response);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning($"Http GET at {release.Url} failed with status code {response.StatusCode}");
                return dependencies;
            }

            ZipArchive package = new ZipArchive(await response.Content.ReadAsStreamAsync());

            var entry = package.GetEntry($"{name.Replace('-', '_')}-{version}.dist-info/METADATA");

            // If there is no metadata file, the package doesn't have any declared dependencies
            if (entry == null)
            {
                return dependencies;
            }

            List<string> content = new List<string>();
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

        public async Task<SortedDictionary<string, IList<PythonProjectRelease>>> GetReleases(PipDependencySpecification spec)
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

                    Logger.LogWarning($"Received {(int)result.Result.StatusCode} {result.Result.ReasonPhrase} from {requestUri}. Waiting {timeSpan} before retry attempt {retryCount}");

                    Interlocked.Increment(ref retries);
                })
                .ExecuteAsync(() =>
                {
                    if (Interlocked.Read(ref retries) >= MAXRETRIES)
                    {
                        return Task.FromResult<HttpResponseMessage>(null);
                    }

                    return GetPypiResponse(requestUri);
                });

            request = await CachePypiResponse(requestUri, request);

            if (request == null)
            {
                using var r = new PypiMaxRetriesReachedTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray() };

                Logger.LogWarning($"Call to pypi.org failed, but no more retries allowed!");

                return new SortedDictionary<string, IList<PythonProjectRelease>>();
            }

            if (!request.IsSuccessStatusCode)
            {
                using var r = new PypiFailureTelemetryRecord { Name = spec.Name, DependencySpecifiers = spec.DependencySpecifiers?.ToArray(), StatusCode = request.StatusCode };

                Logger.LogWarning($"Received {(int)request.StatusCode} {request.ReasonPhrase} from {requestUri}");

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
                    Logger.LogError($"Component {release.Key} : {JsonConvert.SerializeObject(release.Value)} could not be added to the sorted list of pip components. Error details follow:");
                    Logger.LogException(ae, true);
                    continue;
                }
            }

            return versions;
        }
    }
}
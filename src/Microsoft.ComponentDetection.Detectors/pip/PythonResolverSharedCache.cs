namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;

public static class PythonResolverSharedCache
{
    private const long DEFAULTCACHEENTRIES = 4096;
    private static readonly MemoryCache SharedCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });

    /// <summary>
    /// Adds a uri and its response to the cache.
    /// </summary>
    /// <param name="uri"> The uri to store.</param>
    /// <param name="content">The content to store.</param>
    public static void AddToCache(Uri uri, HttpResponseMessage content)
    {
        SharedCache.CreateEntry(uri).Value = content;
    }

    /// <summary>
    /// Get a response from the cache.
    /// </summary>
    /// <param name="uri">The uri to retrieve.</param>
    /// <returns> A HttpResponseMessage. </returns>
    public static HttpResponseMessage GetFromCache(Uri uri)
    {
        if (SharedCache.TryGetValue(uri, out HttpResponseMessage content))
        {
            return content;
        }

        return null;
    }
}

namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;

public static class PythonResolverSharedCache
{
    private const long DEFAULTCACHEENTRIES = 4096;
    private static readonly MemoryCache SharedProjectCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });
    private static readonly MemoryCache SharedWheelFileCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = DEFAULTCACHEENTRIES });

    /// <summary>
    /// Adds a uri and its response to the cache.
    /// </summary>
    /// <param name="specName"> The specName to store.</param>
    /// <param name="content">The content to store.</param>
    public static void AddToProjectCache(string specName, HttpResponseMessage content)
    {
        SharedProjectCache.CreateEntry(specName).Value = content;
    }

    /// <summary>
    /// Get a response from the cache.
    /// </summary>
    /// <param name="specName">The spec name to retrieve.</param>
    /// <returns> A HttpResponseMessage. </returns>
    public static HttpResponseMessage GetFromProjectCache(string specName)
    {
        if (SharedProjectCache.TryGetValue(specName, out HttpResponseMessage content))
        {
            return content;
        }

        return null;
    }

    /// <summary>
    /// Adds a uri and its response to the cache.
    /// </summary>
    /// <param name="uri"> The uri to store.</param>
    /// <param name="content">The content to store.</param>
    public static void AddToWheelFileCache(Uri uri, HttpResponseMessage content)
    {
        SharedWheelFileCache.CreateEntry(uri).Value = content;
    }

    /// <summary>
    /// Get a response from the cache.
    /// </summary>
    /// <param name="uri">The uri to retrieve.</param>
    /// <returns> A HttpResponseMessage. </returns>
    public static HttpResponseMessage GetFromWheelFileCache(Uri uri)
    {
        if (SharedWheelFileCache.TryGetValue(uri, out HttpResponseMessage content))
        {
            return content;
        }

        return null;
    }
}

# Environment Variables

Environment variables are sometimes used to control experimental features or advanced options

## `EnableGoCliScan`

If the environment variable `EnableGoCliScan` is set, to any value, the Go detector uses [`go mod graph`][1] to discover Go dependencies.
If the environment variable is not set, we fall back to parsing `go.mod` and `go.sum` ourselves.

## `PyPiMaxCacheEntries`

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 128.

[1]: https://go.dev/ref/mod#go-mod-graph

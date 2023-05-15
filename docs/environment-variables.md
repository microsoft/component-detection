# Environment Variables

Environment variables are sometimes used to control experimental features or advanced options

## `DisableGoCliScan`

If the environment variable `DisableGoCliScan` is set to "true", we fall back to parsing `go.mod` and `go.sum` ourselves. 
Otherwise, the Go detector uses go-cli command: `go list -m all` to discover Go dependencies.

## `PyPiMaxCacheEntries`

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 4096.

[1]: https://go.dev/ref/mod#go-mod-graph

# Environment Variables

Environment variables are sometimes used to control experimental features or advanced options

## `DisableGoCliScan`

If the environment variable `DisableGoCliScan` is set to "true", we fall back to parsing `go.mod` and `go.sum` ourselves. 
Otherwise, the Go detector uses go-cli command: `go list -m all` to discover Go dependencies.

## `PyPiMaxCacheEntries`

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 128.

[1]: https://go.dev/ref/mod#go-mod-graph

## `CD_LOCKFILE_V3_ENABLED`

If the environment variable `CD_LOCKFILE_V3_ENABLED` is set to "true", this will enable the `NpmDetectorWithRoots` to use the experiementental `package-lock.json` `lockfileVersion` 3 logic. Otherwise, the `package-lock.json` file will be parsed with the existing logic, which is broken on `lockfileVersion` 3.

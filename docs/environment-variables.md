# Environment Variables

Environment variables are sometimes used to control experimental features or advanced options

## `DisableGoCliScan`

If the environment variable `DisableGoCliScan` is set to "true", we fall back to parsing `go.mod` and `go.sum` ourselves. 
Otherwise, the Go detector uses go-cli command: `go list -m all` to discover Go dependencies.

## `PyPiMaxCacheEntries`

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 4096.

## `CD_DETECTOR_EXPERIMENTS`

When set to any value, enables detector experiments, a feature to compare the results of different detectors for the
same ecosystem. The available experiments are found in the [`Experiments\Config`](../src/Microsoft.ComponentDetection.Orchestrator/Experiments/Configs)
folder.

## `CD_GRADLE_DEV_LOCKFILES`

Enables dev-dependency categorization for the Gradle
detector. Comma-separated list of Gradle lockfiles which contain only
development dependencies.  Dependencies connected to Gradle
configurations matching the given regex are considered development
dependencies. If a lockfile will contain a mix of development and
production dependencies, see `CD_GRADLE_DEV_CONFIGURATIONS` below.

## `CD_GRADLE_DEV_CONFIGURATIONS`

Enables dev-dependency categorization for the Gradle
detector. Comma-separated list of Gradle configurations which refer to development dependencies.
Dependencies connected to Gradle configurations matching
the given configurations are considered development dependencies. 
If an entire lockfile will contain only dev dependencies, see `CD_GRADLE_DEV_LOCKFILES` above.

[1]: https://go.dev/ref/mod#go-mod-graph

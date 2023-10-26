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

## `GRADLE_PROD_CONFIGURATIONS_REGEX`

Enables dev-dependency categorization for the Gradle
detector. Dependencies connected to Gradle configurations NOT matching
the given regex are considered development dependencies.

## `GRADLE_DEV_CONFIGURATIONS_REGEX`

Enables dev-dependency categorization for the Gradle
detector. Dependencies connected to Gradle configurations matching
the given regex are considered development dependencies. 

[1]: https://go.dev/ref/mod#go-mod-graph

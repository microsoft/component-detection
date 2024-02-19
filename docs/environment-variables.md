# Environment Variables

Environment variables are sometimes used to control experimental features or advanced options

## `DisableGoCliScan`

If the environment variable `DisableGoCliScan` is set to "true", we fall back to parsing `go.mod` and `go.sum` ourselves. 
Otherwise, the Go detector uses go-cli command: `go list -m all` to discover Go dependencies. [^1]

## `PyPiMaxCacheEntries`

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 4096.

## `CD_DETECTOR_EXPERIMENTS`

When set to any value, enables detector experiments, a feature to compare the results of different detectors for the
same ecosystem. The available experiments are found in the [`Experiments\Config`](../src/Microsoft.ComponentDetection.Orchestrator/Experiments/Configs)
folder.

## `CD_RUST_CLI_FEATURES`

Specifies the features (comma seperated) to be passed into `cargo metadata`, which tells cargo to build the project with
the features for the workspace/project. By default, `cargo metadata` will run with `--all-features`, instructing `cargo`
to determine the build graph if all features should be built in the project/workspace. [^2]

[^1]: https://go.dev/ref/mod#go-mod-graph
[^2]: https://doc.rust-lang.org/cargo/commands/cargo-metadata.html#feature-selection

# Environment Variables

Environment variables are sometimes used to control experimental features or advanced options

## `DisableGoCliScan`

If the environment variable `DisableGoCliScan` is set to "true", we fall back to parsing `go.mod` and `go.sum` ourselves.
Otherwise, the Go detector uses go-cli command: `go list -m all` to discover Go dependencies.

## `DisableRustCliScan`

When set to "true", the Rust detector skips all Cargo CLI execution, including metadata queries.
In SBOM mode, dependencies will be mapped directly to `*.cargo-sbom.json` files.
In fallback mode, the detector processes `Cargo.lock` and `Cargo.toml` files to infer dependencies and relationships.
Disabling the CLI may reduce the precision of component-to-location mapping and can lead to over-reporting.

## `PyPiMaxCacheEntries`

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 4096.

## `PIP_INDEX_URL`

Determines what package feed should be used for `pip install --report` detection.
The default value will use the PyPi index unless pip defaults have been configured globally.

## `PipReportOverrideBehavior`

Overrides pip report with one of the following detection strategies:
- `Skip`: Will not run pip detection
- `SourceCodeScan`: Scan `setup.py` and `requirements.txt` files, and record components explicitly from the package files without hitting a remote feed. Does not compile a dependency graph.

## `PipReportSkipFallbackOnFailure`

When set to "true", skips the default fallback behavior if pip report fails.
Default behavior scans `setup.py` and `requirements.txt` files, and records components explicitly from the package files without hitting a remote feed.
Does not compile a dependency graph.

## `PipReportFileLevelTimeoutSeconds`

Controls the timeout limit (in seconds) for generating the PipReport for individual files.
This defaults to the overall timeout.

## `PipReportDisableFastDeps`

When set to "true", disables the fast deps feature in PipReport.

## `PipReportIgnoreFileLevelIndexUrl`

When set to "true", ignores the `--index-url` argument that can be provided in the requirements.txt file.
See [pip install documentation](https://pip.pypa.io/en/stable/cli/pip_install/#install-index-url) for more details.

## `PipReportPersistReports`

When set to "true", allows the PipReport detector to persist the reports that it generates, rather than cleaning them up after constructing the dependency graph.

## `CD_DETECTOR_EXPERIMENTS`

When set to any value, enables detector experiments, a feature to compare the results of different detectors for the same ecosystem.
The available experiments are found in the [`Experiments\Config`](../src/Microsoft.ComponentDetection.Orchestrator/Experiments/Configs) folder.

## `CD_GRADLE_DEV_LOCKFILES`

Enables dev-dependency categorization for the Gradle detector.
Comma-separated list of Gradle lockfiles which contain only development dependencies.
Dependencies connected to Gradle configurations matching the given regex are considered development dependencies.
If a lockfile will contain a mix of development and production dependencies, see `CD_GRADLE_DEV_CONFIGURATIONS` below.

## `CD_GRADLE_DEV_CONFIGURATIONS`

Enables dev-dependency categorization for the Gradle detector.
Comma-separated list of Gradle configurations which refer to development dependencies.
Dependencies connected to Gradle configurations matching the given configurations are considered development dependencies.
If an entire lockfile will contain only dev dependencies, see `CD_GRADLE_DEV_LOCKFILES` above.

## `MvnCLIFileLevelTimeoutSeconds`

When set to any positive integer value, it controls the max execution time Mvn CLI is allowed to take per each `pom.xml` file.
Default behavior is unbounded.

# Go Detection

## Requirements

Go detection depends on the following to successfully run:

- `go.mod` or `go.sum` files

## Detection strategy

Go detection is performed by parsing any `go.mod` or `go.sum` found under the scan directory.

Only root dependency information is generated instead of full graph.
I.e. tags the top level component or explicit dependency a given transitive dependency was brought by.
Given a dependency tree A -> B -> C, C's root dependency is A.

### Improved detection accuracy via opt-in

**To enable improved detection accuracy, create an environment variable named `EnableGoCliScan` with any value.**

Improved go detection depends on the following to successfully run:

- Go v1.11+.

Go detection is performed by parsing output from executing `go mod graph`.
Full dependency graph generation is supported if Go v1.11+ is present on the build agent.
If no Go v1.11+ is present, fallback detection strategy is performed.

As we validate this opt-in behavior, we will eventually graduate it to the default detection strategy.

## Known limitations

Dev dependency tagging is not supported.

Go detection will fallback if no Go v1.11+ is present.

Due to the nature of `go.sum` containing references for all dependencies, including historical, no-longer-needed dependencies; the fallback strategy can result in over detection.
Executing `go mod tidy` before detection via fallback is encouraged.

## Environment Variables

If the environment variable `EnableGoCliScan` is set, to any value, the Go detector uses [`go mod graph`][1] to discover Go dependencies.
If the environment variable is not present, we fall back to parsing `go.mod` and `go.sum` ourselves.

[1]: https://go.dev/ref/mod#go-mod-graph

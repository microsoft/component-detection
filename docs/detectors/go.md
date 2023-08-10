# Go Detection

## Requirements

Go detection depends on the following to successfully run:

- `go.mod` or `go.sum` files

## Default Detection strategy

Go detection depends on the following to successfully run:

- Go v1.11+.

Full dependency graph generation is supported if Go v1.11+ is present on the build agent.
If no Go v1.11+ is present, fallback detection strategy is performed.

Go detection is performed by parsing output from executing [go list -m -json all](1). To generate the graph, the command [go mod graph](2) is executed, this only adds edges between the components that were already registered by `go list`.

## Fallback Detection strategy
Go detection is performed by parsing any `go.mod` or `go.sum` found under the scan directory.

Only root dependency information is generated instead of full graph.
I.e. tags the top level component or explicit dependency a given transitive dependency was brought by.
Given a dependency tree A -> B -> C, C's root dependency is A.

**To force fallback detection strategy, create set environment variable `DisableGoCliScan=true`.**

## Known limitations
- If the default strategy is used and go modules are not present in the system before the detector is executed, the go cli will fetch all modules to generate the dependency graph. This will incur additional detector time execution.

- Dev dependency tagging is not supported.

- Go detection will fallback if no Go v1.11+ is present.

- Due to the nature of `go.sum` containing references for all dependencies, including historical, no-longer-needed dependencies; the fallback strategy can result in over detection.
Executing [go mod tidy](https://go.dev/ref/mod#go-mod-tidy) before detection via the fallback strategy is encouraged.

- Some legacy dependencies may report stale transitive dependencies in their manifests, in this case you can remove them safely from your binaries by using [exclude directive](https://go.dev/doc/modules/gomod-ref#exclude).

## Environment Variables

If the environment variable `DisableGoCliScan` is set to `true`, the Go detector parses `go.mod` and `go.sum` to discover dependencies. otherwise, it executes default strategy.

[1]: https://go.dev/ref/mod#go-list-m
[2]: https://go.dev/ref/mod#go-mod-graph

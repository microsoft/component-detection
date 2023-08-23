# Go Detection

## Requirements

Go detection runs when one of the following files is found in the project:

- `go.mod` or `go.sum`

## Default Detection strategy

Default Go detection depends on the following to successfully run:

- Go v1.11+.

Full dependency graph generation is supported if Go v1.11+ is present on the build agent.
If no Go v1.11+ is present, fallback detection strategy is performed.

Go detection is performed by parsing output from executing [go list -mod=readonly -m -json all](1). To generate the graph, the command [go mod graph](2) is executed. This only adds edges between the components that were already registered by `go list`.

## Fallback Detection strategy
Go detection is performed by parsing any `go.mod` or `go.sum` found under the scan directory.

Only root dependency information is generated in the fallback detection strategy. The full graph is not detected. Given a dependency tree `A->B->C`, only `A`, `C`'s root dependency, is detected.

To force fallback detection strategy, set the following environment variable: `DisableGoCliScan=true`

## Troubleshooting failures to run the default detection strategy
The fallback detection strategy is known to overreport by nature of parsing `go.sum` files. To ensure the newer, more accurate default detection strategy runs successfully, it is encouraged to do the following:  

1. Ensure that Go v1.11+ is installed on the build agent.  

1. Resolve `go list` errors. Errors are logged in the Component Detection build task output and begin with `#[error]Go CLI command "go list -m -json all" failed with error:`. These errors are typically caused by version resolution problems or incorrectly formatted `go.mod` files. 

1. Ensure that `DisableGoCliScan` is **not** set to `true`. The variable should not be set or should be set to `false`.

1. Fetch Go modules before the Component Detection build task runs. If modules are not fetched, `go list` will pull the modules and may negatively impact performance.

## Known limitations
- If the default strategy is used and go modules are not present in the system before the detector is executed, the go cli will fetch all modules to generate the dependency graph. This will incur additional detector time execution.

- Dev dependency tagging is not supported.

- Go detection will fallback if no Go v1.11+ is present.

- Due to the nature of `go.sum` containing references for all dependencies, including historical, no-longer-needed dependencies; the fallback strategy can result in over detection.
Executing [go mod tidy](https://go.dev/ref/mod#go-mod-tidy) before detection via the fallback strategy is encouraged.

- Some legacy dependencies may report stale transitive dependencies in their manifests, in this case you can remove them safely from your binaries by using [exclude directive](https://go.dev/doc/modules/gomod-ref#exclude).

## Environment Variables

If the environment variable `DisableGoCliScan` is set to `true`, the Go detector forcibly executes the [fallback strategy](#fallback-detection-strategy).

[1]: https://go.dev/ref/mod#go-list-m
[2]: https://go.dev/ref/mod#go-mod-graph

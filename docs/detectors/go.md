# Go Detection

## Requirements

Go detection depends on the following to successfully run:

- Go v1.11+.

## Detection strategy

Go detection is performed by parsing output from executing `go mod graph`.
Full dependency graph generation is supported if Go v1.11+ is present on the build agent.
If no Go v1.11+ is present, a fallback detection strategy is performed, dependent on:

- One or more `go.mod` or `go.sum` files.

For the fallback strategy:

Go detection is performed by parsing any `go.mod` or `go.sum` found under the scan directory.

Only root dependency information is generated instead of full graph.
I.e. tags the top level component or explicit dependency a given transitive dependency was brought by.
Given a dependency tree A -> B -> C, C's root dependency is A.

## Known limitations

Dev dependency tagging is not supported.

Go detection will fallback if no Go v1.11+ is present.
If executing `go mod graph` takes too long (currently if it takes more than 10 seconds), go detection will fall back.
This can happen if modules are not restored before the scan.

Due to the nature of `go.sum` containing references for all dependencies, including historical, no-longer-needed dependencies; the fallback strategy can result in over detection.
Executing `go mod tidy` before detection via fallback is encouraged.

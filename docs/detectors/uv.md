# uv Detection

## Requirements

[uv](https://docs.astral.sh/uv/) detection relies on a [uv.lock](https://docs.astral.sh/uv/concepts/projects/layout/#the-lockfile) file being present.

## Detection strategy

uv detection is performed by parsing a _uv.lock_ found under the scan directory.

Full dependency graph generation is supported.

Editable root packages (`source = { editable = "." }`) and virtual root packages (`source = { virtual = "." }`) are excluded from detected components, while their dependencies are preserved and promoted to roots in the dependency graph.

Dev dependencies across all dependency groups (e.g., `dev`, `lint`, `test`) are identified via transitive reachability analysis. A package reachable from both production and dev roots is classified as non-dev.

Git-sourced packages are registered as `GitComponent` with the repository URL and commit hash extracted from the lockfile.

## Known limitations

1. Non-root workspace member packages are registered as regular components rather than being filtered out.
2. Lockfile version validation is not performed; only lockfile version 1 has been tested.

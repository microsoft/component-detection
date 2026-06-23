# uv Detection

## Requirements

[uv](https://docs.astral.sh/uv/) detection relies on a [uv.lock](https://docs.astral.sh/uv/concepts/projects/layout/#the-lockfile) file being present.

## Detection strategy

uv detection is performed by parsing a <em>uv.lock</em> found under the scan directory.

Full dependency graph generation is supported.

Dev dependencies across all dependency groups (e.g., `dev`, `lint`, `test`) are identified via transitive reachability analysis. A package reachable from both production and dev roots is classified as non-dev.

Git-sourced packages are registered as `GitComponent` with the repository URL and commit hash extracted from the lockfile.

## Known limitations

1. Editable (`source = { editable = "..." }`) and non-root workspace member packages are registered as regular components rather than being filtered out.
2. Lockfile version validation is not performed; only lockfile version 1 has been tested.

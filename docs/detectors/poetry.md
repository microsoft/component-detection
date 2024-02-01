# Poetry Detection
## Requirements
Poetry detection relies on a poetry.lock file being present.

## Detection strategy
Poetry detection is performed by parsing a <em>poetry.lock</em> found under the scan directory.

## Known limitations
1. Poetry detection will not work if lock files are not being used.
2. Dev dependencies are flagged as normal dependencies since it is not possible to determine whether or not
a dependency is a development dependency via the lockfile alone.

Full dependency graph generation is not supported.

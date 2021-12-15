# Poetry Detection
## Requirements
Poetry detection relies on a poetry.lock file being present.

## Detection strategy
Poetry detection is performed by parsing a <em>poetry.lock</em> found under the scan directory.

## Known limitations
Poetry detection will not work if lock files are not being used.

Full dependency graph generation is not supported.

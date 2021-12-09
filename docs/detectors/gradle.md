# Gradle Detection

## Requirements

Gradle detection depends on the following to successfully run:

- Gradle 7 or prior using [Single File lock](https://docs.gradle.org/6.8.1/userguide/dependency_locking.html#single_lock_file_per_project)
- One or more `.lockfile` files

## Detection strategy

Gradle detection is performed by parsing any `*.lockfile` found under the scan directory.

## Known limitations

Gradle detection will not work if lock files are not being used.

Dev dependency tagging is not supported.

Full dependency graph generation is not supported.

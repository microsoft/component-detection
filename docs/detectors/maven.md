# Maven Detection

## Requirements

Maven detection depends on the following to successfully run:

- Maven CLI as part of your PATH. mvn should be runnable from a given command line.
- Maven Dependency Plugin (installed with Maven).
- One or more `pom.xml` files.

## Detection strategy

Maven detection is performed by running `mvn dependency:tree -f {pom.xml}` for each pom file and parsing down the results.

Components tagged as a test dependency are marked as development dependencies.

Full dependency graph generation is supported.

## Known limitations

Maven detection will not run if `mvn` is unavailable.

## Environment Variables

The environment variable `MvnCLIFileLevelTimeoutSeconds` is used to control the max execution time Mvn CLI is allowed to take per each `pom.xml` file. Default value, unbounded. This will restrict any spikes in scanning time caused by Mvn CLI during package restore. We suggest to restore the Maven packages beforehand, so that no network calls happen when executing "mvn dependency:tree" and the graph is captured quickly.

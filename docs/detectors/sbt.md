# SBT Detection

## Requirements

SBT detection depends on the following to successfully run:

- SBT CLI available via system PATH or Coursier distribution
  - On Windows, detector checks: `sbt` command, then `C:\Users\{user}\AppData\Local\Coursier\data\bin\sbt.bat`
  - On other platforms, checks system PATH for `sbt` command
- One or more `build.sbt` files

**Note**: The `sbt-dependency-graph` plugin is no longer required. The detector uses SBT's built-in `dependencyTree` task.

## Detection strategy

SBT detection is performed by running `sbt dependencyTree` for each `build.sbt` file and parsing the tree output. The detector applies a multi-stage filtering process to clean the output:

1. Removes SBT metadata (`[info]`, `[warn]`, `[error]` prefixes)
2. Removes Scala version suffixes from artifact names (e.g., `_2.13`)
3. Removes root component markers (`[S]` suffix)
4. Validates Maven coordinates (requires at least one dot in groupId per Maven convention)
5. Inserts default `jar` packaging to match Maven coordinate format: `group:artifact:jar:version`

The detector leverages the same Maven-style dependency graph parser used by the Maven detector, as SBT dependencies use Maven coordinates (groupId:artifactId:version) and output in a compatible tree format.

Components are registered as Maven components since Scala projects publish to Maven repositories and use the same artifact coordinate system.

Components tagged as a test dependency are marked as development dependencies.

Full dependency graph generation is supported.

## Known limitations

- SBT detection will not run if `sbt` CLI is not available in the system PATH or Coursier distribution
- Only the compile-scope dependencies are scanned by default (test dependencies may be detected as development dependencies if they appear in the dependency tree output)
- Multi-project builds (nested `build.sbt` files) are detected, with parent projects taking precedence
- First invocation of SBT may be slow due to JVM startup and dependency resolution; subsequent runs benefit from cached dependencies

## Environment Variables

The environment variable `SbtCLIFileLevelTimeoutSeconds` is used to control the max execution time SBT CLI is allowed to take per each `build.sbt` file. Default value: unbounded. This will restrict any spikes in scanning time caused by SBT CLI during dependency resolution. 

We suggest running `sbt update` beforehand to ensure dependencies are cached, so that no network calls happen when executing the dependency tree command and the graph is captured quickly.

## Example build.sbt

```scala
name := "MyScalaProject"
version := "0.1"
scalaVersion := "3.3.0"

libraryDependencies ++= Seq(
  "org.typelevel" %% "cats-core" % "2.9.0",
  "org.scalatest" %% "scalatest" % "3.2.15" % Test
)
```

## Integration with Scala Projects

This detector enables Component Detection to scan Scala projects built with SBT, which is the standard build tool for Scala. Since Scala libraries are published to Maven Central and use Maven-style coordinates, detected components are registered as `MavenComponent` types with the appropriate groupId, artifactId, and version.

The `%%` operator in SBT automatically appends the Scala version to the artifact ID (e.g., `cats-core_3` for Scala 3.x), which will be reflected in the detected component names.

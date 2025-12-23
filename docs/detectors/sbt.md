# SBT Detection

## Requirements

SBT detection depends on the following to successfully run:

- SBT CLI as part of your PATH. `sbt` should be runnable from a given command line.
- sbt-dependency-graph plugin (recommended to be added globally or in the project's `project/plugins.sbt`).
- One or more `build.sbt` files.

## Detection strategy

SBT detection is performed by running `sbt "dependencyTree; export compile:dependencyTree > bcde.sbtdeps"` for each build.sbt file and parsing the results. The detector leverages the same Maven-style dependency graph parser used by the Maven detector, as SBT dependencies use Maven coordinates (groupId:artifactId:version).

Components are registered as Maven components since Scala projects publish to Maven repositories and use the same artifact coordinate system.

Components tagged as a test dependency are marked as development dependencies.

Full dependency graph generation is supported.

## Known limitations

- SBT detection will not run if `sbt` is unavailable in the PATH.
- The sbt-dependency-graph plugin must be available. For best results, install it globally in `~/.sbt/1.0/plugins/plugins.sbt`:
  ```scala
  addSbtPlugin("net.virtual-void" % "sbt-dependency-graph" % "0.10.0-RC1")
  ```
- Only the `compile` configuration is scanned by default. Test dependencies may be detected as development dependencies if they appear in the dependency tree output.
- Multi-project builds (nested `build.sbt` files) are detected, with parent projects taking precedence.

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

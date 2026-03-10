# Ivy Detection

## Requirements

Ivy detection depends on the following to successfully run:

- Apache Ant CLI as part of your PATH (`ant` or `ant.bat` should be runnable from a given command line).
- Java Development Kit (JDK) installed and configured for Ant.
- One or more `ivy.xml` files.
- Optional `ivysettings.xml` files in the same directory as `ivy.xml` for repository configuration.

## Detection strategy

Ivy detection is performed by running Apache Ant to resolve dependencies for each `ivy.xml` file found. The detector:

1. Copies `ivy.xml` (and `ivysettings.xml` if present) to a temporary directory.
2. Creates a synthetic Ant build file with a custom task that invokes Ivy's dependency resolver.
3. Executes `ant resolve-dependencies` to resolve both direct and transitive dependencies.
4. Parses the JSON output produced by the custom Ant task to register components.

Components are identified using Maven's GAV (group, artifact, version) coordinate system, which corresponds to Ivy's (org, name, rev) coordinates. Dependencies with the same organization as the project are treated as first-party dependencies and ignored.

Components tagged as development dependencies are marked appropriately.

Full dependency graph generation is supported.

## Known limitations

Ivy detection will not run if `ant` is unavailable in the PATH.

The `ivy.xml` and `ivysettings.xml` files must be self-contained. Detection will fail if these files:
- Rely on properties defined in the project's `build.xml`
- Use file inclusion mechanisms (e.g., `<include>` tags)

Dependencies that cannot be resolved by Ivy will be logged as package parse failures and not included in the detection results.

# Conan Detection
## Requirements
Conan detection relies on a conan.lock file being present.

## Detection strategy
Conan detection is performed by parsing every **conan.lock** found under the scan directory.

## Known limitations
Conan detection will not work if lock files are not being used or not yet generated. So ensure to run the conan build to generate the lock file(s) before running the scan.

Full dependency graph generation is not supported. However, dependency relationships identified/present in the **conan.lock** file is captured.

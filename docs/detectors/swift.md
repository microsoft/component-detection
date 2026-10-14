# Swift Package Manager Detection

## Requirements

Swift Package Manager detection requires the following file to be present in the scan directory:

- `Package.resolved` file

## Detection strategy

The detector `SwiftResolvedComponentDetector` only parses the `Package.resolved` file to get the dependencies.
This file contains a json representation of the resolved dependencies of the project with the transitive dependencies.
The version, the url and commit hash of the dependencies are stored in this file.

[This is the only reference in the Apple documentation to the `Package.resolved` file.][1]

## Experiment results

Swift detection is experimental. The registered `SwiftDetectorExperiment` uses an empty control group
and `SwiftResolvedComponentDetector` as its experiment group, so all Swift discoveries are reported as additions.
It does not depend on CocoaPods detection or compare Swift packages against CocoaPods packages.

Enable experiment processing with `CD_DETECTOR_EXPERIMENTS` or `DetectorExperiments.Enable`.
Scans with no Swift components do not produce experiment results. Disabling the Swift detector also disables this experiment.
The CLI writes results to `Experiment_SwiftDetectorExperiment_{timestamp}_{processId}.json`;
hosts that emit `ExperimentResults` custom events must register their own `IExperimentProcessor`.

## Known limitations

Right now the detector does not support parsing `Package.swift` file to get the dependencies.
It only supports parsing `Package.resolved` file.
Some projects only commit the `Package.swift`, which is why it is planned to support parsing `Package.swift` in the future.

[1]: https://docs.swift.org/package-manager/PackageDescription/PackageDescription.html#package-dependency

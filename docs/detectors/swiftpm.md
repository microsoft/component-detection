# Go Detection

## Requirements

Swift Package Manager detection requires the following file to be present in the scan directory:

-   `Package.resolved` file

## Detection strategy

The detector `SwiftPMResolvedComponentDetector` only parses the `Package.resolved` file to get the dependencies.
This file contains a json representation of the resolved dependencies of the project with the transitive dependencies.
The version, the url and commit hash of the dependencies are stored in this file. 

[This is the only reference in the Apple documentation to the `Package.resolved` file.][1]


## Known limitations

Right now the detector does not support parsing `Package.swift` file to get the dependencies. 
It only supports parsing `Package.resolved` file. 
Some projects only commit the `Package.swift`, which is why it is planned to support parsing `Package.swift` in the future.

[1]: https://docs.swift.org/package-manager/PackageDescription/PackageDescription.html#package-dependency

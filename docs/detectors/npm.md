# Npm Detection

## Requirements

There are multiple detectors that make up npm detection and each detector searches for the following files:

- [The `NpmComponentDetector` detector searches for `package.json`][1]
- [The `NpmComponentDetectorWithRoots` and `NpmLockfile3Detector` detectors search for `package-lock.json`, `npm-shrinkwrap.json`, and `lerna.json`][2]

## Detection strategy

npm detectors search for dependencies in `packages.json`, `package-lock.json`, `npm-shrinkwrap.json` and `lerna.json` in the scan directory.
The lockfile detectors (`NpmComponentDetectorWithRoots` and `NpmLockfile3Detector`) are able to scan for transitive dependencies within the project.
There is also an extension of the lockfile detector the NpmLockFilev3 detector that is able to scan [version 3 of lockfiles][3]

## Known limitations

Npm supports [`optionalDependencies`][4] which can cause an overreporting issue with the detector
However, this is not much of an issue as the majority of projects only use `dependencies` and `devDependencies`.

[1]: https://github.com/microsoft/component-detection/blob/251276d7951c7eaa880ed58b1a974b25dba92cd2/src/Microsoft.ComponentDetection.Detectors/npm/NpmComponentDetector.cs#L36
[2]: https://github.com/microsoft/component-detection/blob/251276d7951c7eaa880ed58b1a974b25dba92cd2/src/Microsoft.ComponentDetection.Detectors/npm/NpmLockfileDetectorBase.cs#L52
[3]: https://github.com/microsoft/component-detection/blob/251276d7951c7eaa880ed58b1a974b25dba92cd2/src/Microsoft.ComponentDetection.Detectors/npm/NpmLockfile3Detector.cs#L36
[4]: https://docs.npmjs.com/cli/v9/configuring-npm/package-json#optionaldependencies

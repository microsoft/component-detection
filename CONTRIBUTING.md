# Contributing to Component Detection

This project welcomes contributions and suggestions. Most contributions require you to
agree to a Contributor License Agreement (CLA) declaring that you have the right to,
and actually do, grant us the rights to use your contribution. For details, visit
https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need
to provide a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the
instructions provided by the bot. You will only need to do this once across all repositories using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/)
or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

We strive to make this codebase as contributor friendly as possible to encourage teams, which are subject matter experts on the tooling they use every day, to contribute back and improve the experience for everyone. If there is a bug fix or feature you would like to contribute, please follow the guidelines below and let us know how you would like to contribute.

For bugs, issues, and support please email [OpenSourceEngSupport@microsoft.com](mailto:OpenSourceEngSupport@microsoft.com)

First, let's get to know the file structure.

# File structure

## [.github/workflows](.github/workflows)
All CI / CD is handled through github actions. Most are self-explanatory, but a few interesting ones exist:
* [test-linux.yml](.github/workflows/test-linux.yml) -- This is used specifically to test the linux detector, which must be run on *nix variant systems until tern supports windows in a first class way.
* [verify-snapshot.yml](.github/workflows/verify-snapshot.yml) -- This is essentially an "end to end" test, using the componentdetection-verification repo as a baseline. It looks at scan output captured from [publish-release-snapshot.yml](./github/workflows/publish-release-snapshot.yml) and compares it to the output being created by the changes in the current PR. Because of the end to end nature of the test, this workflow can be a little less stable (e.g. components detected can change for ecosystems that don't have locked deps).

## [src](src)
All dotnet core code that is used to run component detection.

### [src/Microsoft.ComponentDetection](src/Microsoft.ComponentDetection)
The entry point for the dotnet application. Handles the --Debug argument and does little else.
### [src/Microsoft.ComponentDetection.Orchestrator](src/Microsoft.ComponentDetection.Orchestrator)
The code that pulls together the arguments ands runs different commands based on input, deciding which services to call and wrapping the detectors so they don't break everything. Generally, the app "glue".
### [src/Microsoft.ComponentDetection.Common](src/Microsoft.ComponentDetection.Common)
Code shared by everything in the app, not including Contracts.
### [src/Microsoft.ComponentDetection.Contracts](src/Microsoft.ComponentDetection.Contracts)
Models used in the solution, used to deserialize / reserialize output from the application.
### [src/Microsoft.ComponentDetection.Detectors](src/Microsoft.ComponentDetection.Detectors)
bulk of the code that actually runs detection -- each built-in detector should have it's own folder (usually by ecosystem). Ecosystem specific utilities live adjacent to the detector implementation itself. [Additional documentation on the linux detector.](./docs/linux-scanner.md)

# What is a Detector?
The bulk of contributions/work will be in this area. Currently, there are two kinds of detector. The main kind, 'FileDetector', relies on traversing the file directory and finding a specified file to parse and discover what components to register. The set of file names for a detector is specified as a part of the class variables, and the orchestrator runs a file scanning process, sending the results to the appropriate detectors. The other kind of detector, `IComponentDetector`, isn't necessarily file focused (though it can be) but is generally intended to do "one-shot" scanning. An example of this is the LinuxDetector that takes scan arguments from the command line. Since it doesn't have to discover new docker images to scan and it doesn't operate over any files, it simply implements `IComponentDetector`.

The detector returns the set of all components found by the detector, typically mapped by its locations and in some cases including the dependency chain (one way).

## Contributing a new detector

Please see [creating a new detector](./docs/creating-a-new-detector.md).

### PR Policies
* Branch names should follow `{user}/{title}` convention
    * eg: tevoinea/IssueTemplate
* All checks must pass
* At least 1 required review

### Style

Analysis rulesets are defined in [analyzers.ruleset](analyzers.ruleset) and validated by our PR builds.

### Testing

L0s are defined in `MS.VS.Services.Governance.CD.*.L0.Tests`.

Verification tests are run on the sample projects defined in [microsoft/componentdetection-verification](https://github.com/microsoft/componentdetection-verification).

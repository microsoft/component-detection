
# Component Detection
![Component Detection CI](https://github.com/microsoft/componentdetection-bcde/workflows/Component%20Detection%20CI/badge.svg)

**For bugs, issues, and support please create an issue.**

# Introduction

ComponentDetection (BCDE) is a package scanning tool intended to be used at build time. BCDE produces a graph-based output of all detected components and supports a variety of open source package ecosystems.

# Table of Contents

* [Feature Overview](#Feature-Overview)
* [My favorite language/ecosystem isn't supported!](#My-favorite-language/ecosystem-isn't-supported!)
* [Building and running BCDE](#Building-and-running-BCDE)
    * [Running in Visual Studio (2019+)](#Running-in-Visual-Studio-(2019+))
	* [Running from command line](#Running-from-command-line)
	* [After building](#After-building)
* [A detector is marked as DefaultOff/Experimental. What does that mean?](#A-detector-is-marked-as-DefaultOff/Experimental.-What-does-that-mean?)
* [Telemetry](#Telemetry)

# Feature Overview

| Ecosystem | Scanning | Graph Creation |
| - | - | - |
| CocoaPods | ✔ | ✔ |
| Linux (Debian, Alpine, Rhel, Centos, Fedora, Ubuntu)| ✔ (via [syft](https://github.com/anchore/syft)) | ❌ |
| Gradle (lockfiles only) | ✔ | ❌ |
| Go | ✔ | ❌ |
| Maven | ✔ | ✔ |
| NPM (including Yarn, Pnpm) | ✔ | ✔ |
| NuGet | ✔ | ✔ |
| Pip (Python) | ✔ | ✔ |
| Ruby | ✔ | ✔ |
| Rust | ✔ | ✔ |

For a complete feature overview refer to [feature-overview.md](docs/feature-overview.md)

# My favorite language/ecosystem isn't supported!

BCDE is built with extensibility in mind! Please see our [CONTRIBUTING.md](CONTRIBUTING.md) to get started where you can find additional docs on adding your own detector.


# Building and running BCDE
DotNet Core SDK 6.0.0-rc2 is currently in use, you can install it from https://dotnet.microsoft.com/download/dotnet/6.0
We also use node and npm, you can install them from https://nodejs.org/en/download/

The below commands mirror what we do to setup our CI environments:

From the base folder:
``` dotnet build ```

## Running in Visual Studio (2019+)
1. open [ComponentDetection.sln](ComponentDetection.sln) in Visual Studio
1. Set the Loader project as the startup project (rightclick-> Set as Startup Project)
1. Set Run arguments for the Loader project (rightclick->properties->Debug)  
	*Minimum:* `scan --SourceDirectory <Repo to scan>`
1. Now, any time you make a change, you can press `F5`. This will build the changes, and start the process in debug mode (hitting any breakpoints you set)

## Using Codespaces

If you have access to [GitHub Codespaces](https://docs.github.com/en/free-pro-team@latest/github/developing-online-with-codespaces/about-codespaces), select the `Code` button from the [repository homepage](https://github.com/microsoft/componentdetection-bcde) then select `Open with Codespaces`. That's it! You have a full developer environment that supports debugging, testing, auto complete, jump to definition, everything you would expect.

## Using VS Code DevContainer

This is similar to Codespaces:

1. Make sure you meet [the requirements](https://code.visualstudio.com/docs/remote/containers#_getting-started) and follow the installation steps for DevContainers in VS Code
1. `git clone https://github.com/microsoft/componentdetection-bcde`
1. Open this repo in VS Code
1. A notification should popup to reopen the workspace in the container. If it doesn't, open the [`Command Palette`](https://code.visualstudio.com/docs/getstarted/tips-and-tricks#_command-palette) and type `Remote-Containers: Reopen in Container`.

## Running from command line
The most basic run:
```
dotnet run --project src/Microsoft.ComponentDetection scan --SourceDirectory .\ 
```
You can add `--no-restore` or `--no-build` if you don't want to rebuild before the run
	
You can add `--Debug` to get the application to wait for debugger attachment to complete.

## After building
Additional arguments for detection can be found in [detector arguments](docs/detector-arguments.md)

# A detector is marked as DefaultOff/Experimental. What does that mean?

Detectors have 3 levels of "stability":
* `DefaultOff`
* `Experimental`
* `Stable`

DefaultOff detectors need to be explicitly enabled to run and produce a final graph output. Experimental detectors run by default but **will not** produce a final graph output. Stable detectors run and produce a final graph output by default. Here is how you can [enable default off/experimental](./docs/enable-default-off.md) detectors.

# Telemetry
By default, telemetry will output to your output file path and will be a JSON blob. No data is submitted to Microsoft.

# Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/)
or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

# Trademarks
This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow Microsoft's Trademark & Brand Guidelines. Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party's policies.

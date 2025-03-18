# NuGet Detection

## Requirements

NuGet Detection depends on the following to successfully run: 

- One or more `*.nuspec`, `*.nupkg`, `*.packages.config`, or `.*csproj` files.
- The files each NuGet detector searches for:  
    - [The `NuGet` detector looks for `*.nupkg`, `*.nuspec`, `nuget.config`, `paket.lock`][1]
    - [The `NuGetPackagesConfig` detector looks for `packages.config`][2]
    - [The `NuGetProjectCentric` detector looks for `project.assets.json`][3]

[1]: https://github.com/microsoft/component-detection/blob/13f3e9f32c94bf6189fbd0bfbdf2e68cc60fccd9/src/Microsoft.ComponentDetection.Detectors/nuget/NuGetComponentDetector.cs#L40
[2]: https://github.com/microsoft/component-detection/blob/13f3e9f32c94bf6189fbd0bfbdf2e68cc60fccd9/src/Microsoft.ComponentDetection.Detectors/nuget/NuGetPackagesConfigDetector.cs#L25
[3]: https://github.com/microsoft/component-detection/blob/13f3e9f32c94bf6189fbd0bfbdf2e68cc60fccd9/src/Microsoft.ComponentDetection.Detectors/nuget/NuGetProjectModelProjectCentricComponentDetector.cs#L205

## Detection Strategy 

NuGet Detection is performed by parsing any `*.nuspec`, `*.nupkg`, `*.packages.config`, or `*.project.assets` files found under the scan directory. By searching for all `*.nuspec,` `*.nupkg` files on disk the global NuGet cache gets searched which can include packages that are not included in the final build.

## NuGetProjectCentric

The `NuGetProjectCentric` detector raises NuGet components referenced by projects that use the latest NuGet (v3 or later) and build-integrated `PackageReference` [items][4].  These components represent both direct dependencies and transitive dependencies brought in by references from direct package and project references.  Packages that contribute no assets to the project or exclusively contribute [Compile assets][5] are treated as development dependencies.

The .NET SDK will perform conflict resolution for all packages during the build.  This process will remove assets from packages that overlap with assets of the same name that come from the .NET framework that's used by the project.  For example if a project references `System.Text.Json` version `6.0.0` and targets `net8.0` which includes a newer `System.Text.Json` the .NET SDK will ignore all the assets provided by the `System.Text.Json` package and only use those provided by the framework.  Unfortunately the result of this process is not persisted in any build artifact.  To approximate this we capture a list of packages per framework version that would lose to the framework assets.  When examining packages referenced by a project for a given framework, if we find that its included in the list we'll mark it as a development dependency.

Future versions of the .NET SDK have moved this framework conflict resolution into NuGet via the [PrunePackageReference feature][6].  This feature will apply similar rules to conflict resolution during restore and avoid even downloading the package.  As a result the packages will not appear at all in the assets file since they are no longer used.

[4]: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files
[5]: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets
[6]: https://github.com/NuGet/Home/blob/451c27180d14214bca60483caee57f0dc737b8cf/accepted/2024/prune-package-reference.md

## NuGetPackagesConfig

The `NuGetPackagesConfig` detector raises NuGet components referenced by projects or solutions that use the older NuGet (v2) `packages.config` [file][7].

[7]: https://learn.microsoft.com/en-us/nuget/reference/packages-config

## Known Limitations

- Any components that are only found in `*.nuspec` or `*.nupkg` files will not be detected with the latest NuGet Detector approach, because the NuGet detector that scans `*.nuspec` or `*.nupkg` files overreports. This is due to of NuGet's [restore behaviour][8] which downloads all possible dependencies before [resolving the final dependency graph][9].

[8]: https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore#package-restore-behavior
[9]: https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution


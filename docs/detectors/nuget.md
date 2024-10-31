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

## Known Limitations

- Any components that are only found in `*.nuspec` or `*.nupkg` files will not be detected with the latest NuGet Detector approach, because the NuGet detector that scans `*.nuspec` or `*.nupkg` files overreports. This is due to of NuGet's [restore behaviour][4] which downloads all possible dependencies before [resolving the final dependency graph][5].
- Dependencies from the .NET SDK that are underreported. The list of dependencies can be found [here][6].

[4]: https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore#package-restore-behavior
[5]: https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution
[6]: https://github.com/microsoft/component-detection/blob/13f3e9f32c94bf6189fbd0bfbdf2e68cc60fccd9/src/Microsoft.ComponentDetection.Detectors/nuget/NuGetProjectModelProjectCentricComponentDetector.cs#L31-L185


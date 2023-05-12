# NuGet Detection

## Requirements

NuGet Detection depends on the following to successfully run: 

- One or more `*.nuspec`, `*.nupkg`, `*.packages.config`, or `.*csproj` files.

## Detection Strategy 

NuGet Detection is performed by parsing any `*.nuspec`, `*.nupkg`, `*.packages.config`, or `*.project.assets` files found under the scan directory. By searching for all `*.nuspec,` `*.nupkg` files on disk the global NuGet cache gets searched which can include packages that are not included in the final build.

## Known Limitations

Currently the NuGet detector is over-reporting because the global NuGet cache gets searched. To solve this over-reporting a new NuGet Detector approach will be rolled out. This new approach will now only parse `*.packages.config`, or `*.project.assets` (`*.csproj`) files. So any components that are only found in `*.nuspec,` or `*.nupkg` files will not be detected with the new NuGet Detector approach.

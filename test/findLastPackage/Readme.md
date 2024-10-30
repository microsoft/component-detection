A tool for calculating overlaping library packages on past frameworks.

The .NET SDK does conflict resolution with NuGet packages to decide when to ignore the content of packages in favor of that from other sources (like other packages or the Framework itself).

Since packages are immutable - we can precompute what packages will "lose" to framework assets.  To do that we download the package, resolve what assets apply, then compare those to the framework assets.  We use the reference assets only, since that's the minimum version of the framework assets and what conflict resolution itself would use.

The framework targeting packs have a file to ship these precomputed package versions, [PackageOverrides.txt](https://github.com/dotnet/sdk/blob/7deb36232b9c0ccd5084fced1df07920c10a5b72/src/Tasks/Microsoft.NET.Build.Tasks/ResolveTargetingPackAssets.cs#L199) -- but that file wasn't kept up to date of the course framework versions.  It was meant as a "fast path" not a source of truth.  In future framework versions this will need to be the source of truth since it will feed into NuGet's supplied by platform feature.

Once caclculating these we reduce them to a minimum set by allowing compatible frameworks to build upon the previous framework's data - thus reducing the total code size and memory usage of the set of framework packages.

This tool is very special purpose and one-off for this scenario (even generating the exact source format expected by the component-detection types).  Its a means to an end and shared for reference.

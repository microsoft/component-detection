# DotNet SDK Detection

## Requirements

DotNet SDK Detection depends on `project.assets.json` files in the project output / intermediates.

## Detection Strategy 

The `project.assets.json` will be produced when a project is restored and built.  From this we can locate 
the project file that was built.  From the project file location we probe for the .NET SDK version used.  
We look up the directory path to find a `global.json`[1] then will run `dotnet --version` in that 
directory to determine which version of the .NET SDK it will select.  If no `global.json` is found, then 
probing will stop when the detector encounters `SourceDirectory`, `SourceFileRoot`, or the root of the drive 
and proceed to run `dotnet --version` in that directory.  Repositories control the version of the .NET SDK 
used to build their project by either preinstalling on their build machine or container, or acquiring during 
the build pipeline.  The .NET SDK version used is important as this version selects redistributable content 
that becomes part of the application (the dotnet host, runtime for self-contained or AOT apps, build tools 
which generate source, etc).

In addition to recording the SDK version used, the detector will report the framework versions targeted by 
the project as `TargetFramework` values as well as the type of project `application` or `library`.  These 
are important because applications may be built to target old framework versions which may be out of support
and have unreported vulnerabilities.  `TargetFramework` is determined from the `project.assets.json` while 
the type of the project is determined by locating the project's output assembly in a subdirectory of the 
output path and reading the PE COFF header's characteristics for `IMAGE_FILE_EXECUTABLE_IMAGE`[2].

[1]: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
[2]: https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#characteristics

## Known Limitations

If the `dotnet` executable is not on the path the detector may fail to locate the version used to build the 
project.  The detector will fallback to parsing the `global.json` in this case if it is present.
Detection of the output type is done by locating the output assembly under the output path specified in 
`project.assets.json`.  Some build systems may place project intermediates in a different location.  In this
case the project type will be reported as `unknown`.
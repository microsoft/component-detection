# Detector arguments

``` shell
dotnet run --project "src\Microsoft.ComponentDetection\Microsoft.ComponentDetection.csproj" help scan
```

```

  --DirectoryExclusionList    Filters out specific directories following a
                              semicolon separated list of minimatch patterns.

  --IgnoreDirectories         Filters out specific directories, providing
                              individual directory paths separated by semicolon.
                              Obsolete in favor of DirectoryExclusionList's glob
                              syntax.

  --SourceDirectory           Required. Directory to operate on.

  --SourceFileRoot            Directory where source files can be found.

  --DetectorArgs              Comma separated list of properties that can affect
                              the detectors execution, like EnableIfDefaultOff
                              that allows a specific detector that is in beta to
                              run, the format for this property is
                              DetectorId=EnableIfDefaultOff, for example
                              Pip=EnableIfDefaultOff.

  --DetectorCategories        A comma separated list with the categories of
                              components that are going to be scanned. The
                              detectors that are going to run are the ones that
                              belongs to the categories.The possible values are:
                              Npm, NuGet, Maven, RubyGems, Cargo, Pip, GoMod,
                              CocoaPods, Linux.

  --DetectorsFilter           A comma separated list with the identifiers of the
                              specific detectors to be used. This is meant to be
                              used for testing purposes only.

  --ManifestFile              The file to write scan results to.

  --DockerImagesToScan        Comma separated list of docker image names or
                              hashes to execute container scanning on, ex:
                              ubuntu:16.04,
                              56bab49eef2ef07505f6a1b0d5bd3a601dfc3c76ad4460f24c
                              91d6fa298369ab

  --Debug                     Wait for debugger on start

  --DebugTelemetry            Used to output all telemetry events to the
                              console.

  --CorrelationId             Identifier used to correlate all telemetry for a
                              given execution. If not provided, a new GUID will
                              be generated.

  --Verbosity                 (Default: Normal) Flag indicating what level of
                              logging to output to console during execution.
                              Options are: Verbose, Normal, or Quiet.

  --Timeout                   An integer representing the time limit (in
                              seconds) before detection is cancelled

  --Output                    Output path for log files. Defaults to %TMP%

  --PrintManifest             Prints the manifest to standard output.
                              Logging will be redirected to standard error.

  --NoSummary                 Do not display the detection summary on the standard
                              output nor in the logs.

  --AdditionalDITargets       Comma separated list of paths to additional
                              dependency injection targets.

  --help                      Display this help screen.

  --version                   Display version information.
```

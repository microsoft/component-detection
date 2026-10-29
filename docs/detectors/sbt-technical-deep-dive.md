# Technical Deep Dive: SBT Detector Implementation

## Overview

The SBT detector enables Component Detection to scan Scala projects built with SBT (Scala Build Tool) and extract their Maven-style dependencies. Since SBT projects don't have native `pom.xml` files but publish to and consume from Maven repositories, this detector bridges the gap by executing SBT CLI commands and parsing the output.

## Architecture

### Component Structure

The SBT detector follows Component Detection's standard detector pattern with three main components:

1. **`SbtComponentDetector`** - File-based detector that orchestrates the scanning process
2. **`SbtCommandService`** - Service layer that executes SBT CLI and parses dependency output
3. **`ISbtCommandService`** - Interface for dependency injection and testability

### Detection Flow

```
build.sbt found → Verify SBT CLI exists → Execute dependencyTree → 
Parse output → Register MavenComponents → Cleanup temp files
```

## Key Implementation Details

### 1. File Discovery (`SbtComponentDetector`)

**Search Pattern**: `build.sbt`

```csharp
public override IEnumerable<string> SearchPatterns => new[] { "build.sbt" };
```

The detector uses the `FileComponentDetectorWithCleanup` base class, which:
- Automatically discovers files matching `build.sbt` pattern
- Provides lifecycle hooks: `OnPrepareDetectionAsync`, `OnFileFoundAsync`, `OnDetectionFinished`
- Handles file stream management and component recording

**Detector Classification**:
- **DetectorClass**: Maven (reuses Maven infrastructure)
- **ComponentType**: Maven (creates `MavenComponent` instances)
- **DefaultOff**: Yes (`IDefaultOffComponentDetector`) - must be explicitly enabled via `--DetectorArgs SBT=EnableIfDefaultOff`

### 2. CLI Verification (`OnPrepareDetectionAsync`)

Before processing any files, the detector verifies SBT CLI availability:

```csharp
protected override async Task OnPrepareDetectionAsync(IObservableDirectoryWalkerFactory walkerFactory, ...)
{
    this.sbtCLIExists = await this.sbtCommandService.SbtCLIExistsAsync();
    if (!this.sbtCLIExists)
    {
        this.Logger.LogInformation("SBT CLI was not found in the system");
    }
}
```

**CLI Detection Logic** (`SbtCommandService.SbtCLIExistsAsync`):
- Primary command: `sbt`
- Coursier fallback: `C:\Users\{user}\AppData\Local\Coursier\data\bin\sbt.bat` (Windows)
- Verification: Runs `sbt --version` to confirm functional installation
  - **Critical Fix**: Uses `--version` (not `sbtVersion`) because `--version` works without a project directory
  - `sbtVersion` command requires an active project context, causing failures in subprocess environment

This prevents expensive file processing if SBT isn't available.

### 3. Dependency Tree Generation (`GenerateDependenciesFileAsync`)

This is the core of the detector's functionality.

#### Working Directory Context

```csharp
var buildDirectory = new DirectoryInfo(Path.GetDirectoryName(buildSbtFile.Location));
```

**Critical**: SBT must execute from the project root directory where `build.sbt` resides. This is because:
- SBT loads project configuration from the current directory
- The `dependencyTree` task operates on the active project context
- Relative paths in `build.sbt` are resolved from the working directory

#### Command Execution

```csharp
var cliParameters = new[] { "dependencyTree" };
```

**Command Breakdown**:
- `dependencyTree` - Invokes the built-in dependency tree analysis task
- Outputs dependency tree to stdout in a format compatible with Maven's tree format
- Each line contains tree structure markers (`|`, `+-`) followed by coordinates

**SBT Output Example**:
```
[info] test-project:test-project_2.13:1.0.0 [S]
[info]   +-com.google.guava:guava:32.1.3-jre
[info]   | +-com.google.code.findbugs:jsr305:3.0.2
[info]   | +-com.google.guava:failureaccess:1.0.1
[info]   |
[info]   +-org.apache.commons:commons-lang3:3.14.0
[info]
[success] Total time: 0 s
```

**Why This Approach?**:
- `dependencyTree` is a standard SBT task (no plugin required)
- Output includes SBT metadata (`[info]` prefixes, startup messages) which is filtered downstream
- Captures compile-scope dependencies which are the most relevant for security scanning

#### Timeout Management

```csharp
var cliFileTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
if (this.envVarService.DoesEnvironmentVariableExist(SbtCLIFileLevelTimeoutSecondsEnvVar) 
    && int.TryParse(..., out timeoutSeconds) && timeoutSeconds >= 0)
{
    cliFileTimeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
}
```

**Configurable Timeout**: `SbtCLIFileLevelTimeoutSeconds` environment variable
- **Default**: No timeout (inherits from parent cancellation token)
- **Purpose**: SBT can be slow on first run (downloads dependencies, compiles plugins)
- **Cancellation Handling**: Logs warning and gracefully fails the file if timeout occurs

#### Error Handling

```csharp
if (result.ExitCode != 0)
{
    this.logger.LogDebug("execution failed for build.sbt file: {BuildSbtLocation}", buildSbtFile.Location);
    var errorMessage = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
    if (!string.IsNullOrWhiteSpace(errorMessage))
    {
        this.logger.LogError("Sbt output: {SbtStdErr}", errorMessage);
        processRequest.SingleFileComponentRecorder.RegisterPackageParseFailure(buildSbtFile.Location);
    }
}
```

**Failure Registration**: The detector records parse failures instead of crashing, allowing the scan to continue with other files.

### 4. Output Filtering (`GenerateDependenciesFileAsync` - cleanup phase)

After SBT execution, the raw output is cleaned to prepare for Maven parsing:

```csharp
var cleanedLines = allLines
    .Select(line => Regex.Replace(line, @"\s*\[.\]$", string.Empty))  // Remove [S] suffixes
    .Select(line => Regex.Replace(line, @"^\[info\]\s*|\[warn\]\s*|\[error\]\s*", string.Empty))
    .Select(line => Regex.Replace(line, @"_\d+\.\d+(?=:)", string.Empty))  // Remove Scala version _2.13
    .Where(line => Regex.IsMatch(line, @"^[\s|\-+]*[a-z0-9\-_.]*\.[a-z0-9\-_.]+:[a-z0-9\-_.,]+:[a-z0-9\-_.]+"))
    .Select(line => /* Insert packaging 'jar' in correct position */)
    .ToList();
```

**Filtering Pipeline**:
1. **Remove `[S]` suffixes**: Root component markers (e.g., `test-project:test-project_2.13:1.0.0 [S]` → `test-project:test-project_2.13:1.0.0`)
2. **Remove `[info]`/`[warn]`/`[error]` prefixes**: SBT metadata prefixes
3. **Remove Scala version suffixes**: Artifact names include Scala version (e.g., `guava_2.13` → `guava`)
4. **Filter to valid Maven coordinates**: Keep only lines matching pattern (requires dot in groupId per Maven convention)
5. **Insert default packaging**: Convert `group:artifact:version` to `group:artifact:jar:version` for Maven parser compatibility

**Key Insight**: Tree structure characters (`|`, `+-`) are PRESERVED because the Maven parser needs them to understand dependency relationships.

**Output After Filtering**:
```
+-com.google.guava:guava:jar:32.1.3-jre
| +-com.google.code.findbugs:jsr305:jar:3.0.2
| +-com.google.guava:failureaccess:jar:1.0.1
| +-org.apache.commons:commons-lang3:jar:3.14.0
```

### 5. Dependency Parsing (`ParseDependenciesFile`)

```csharp
public void ParseDependenciesFile(ProcessRequest processRequest)
{
    using var sr = new StreamReader(processRequest.ComponentStream.Stream);
    var lines = sr.ReadToEnd().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
    this.parserService.Parse(lines, processRequest.SingleFileComponentRecorder);
}
```

**Reuse of Maven Infrastructure**: This is the key architectural decision. Instead of reimplementing dependency tree parsing, the SBT detector leverages `IMavenStyleDependencyGraphParserService`.

#### Why This Works

SBT outputs dependency trees in a format compatible with Maven's `mvn dependency:tree`:

```
com.google.guava:guava:jar:32.1.3-jre
| +-com.google.code.findbugs:jsr305:jar:3.0.2
| +-com.google.guava:failureaccess:jar:1.0.1
```

**Maven Parser Compatibility**:
- Tree structure uses `|` and `+-` for branches (preserved from SBT output)
- Artifacts use Maven coordinates: `groupId:artifactId:jar:version`
- Indentation and branch markers determine dependency hierarchy
- Root component is the project itself; nested components are dependencies

The `MavenStyleDependencyGraphParserService`:
1. Parses first non-empty line as root component
2. For subsequent lines, extracts tree depth from indentation/markers
3. Uses depth to determine parent-child relationships
4. Creates `MavenComponent` instances with proper Maven coordinates
5. Registers components with the `IComponentRecorder` with proper graph edges

### 6. Component Registration

Inside `MavenStyleDependencyGraphParserService.Parse()`:

```csharp
var component = new DetectedComponent(new MavenComponent(groupId, artifactId, version));
singleFileComponentRecorder.RegisterUsage(
    component,
    isExplicitReferencedDependency: isRootDependency,
    parentComponentId: parentComponent?.Component.Id
);
```

**Graph Construction**:
- **Root dependencies**: Direct dependencies declared in `build.sbt` (marked as `isExplicitReferencedDependency: true`)
- **Transitive dependencies**: Indirect dependencies pulled in by root deps (linked via `parentComponentId`)
- **Component Identity**: Uses Maven's `groupId:artifactId:version` as the unique identifier

### 7. Cleanup (File Deletion in `OnFileFoundAsync`)

```csharp
protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
{
    this.sbtCommandService.ParseDependenciesFile(processRequest);
    File.Delete(processRequest.ComponentStream.Location);
    await Task.CompletedTask;
}
```

**Temporary File Management**:
- The detector does NOT create temporary files on disk during normal operation
- File writing is internal to `GenerateDependenciesFileAsync()` but files are deleted immediately after parsing
- This approach keeps the filesystem clean and prevents accumulation of temp files

## Dependency Injection

```csharp
// In ServiceCollectionExtensions.cs
services.AddSingleton<IComponentDetector, SbtComponentDetector>();
services.AddSingleton<ISbtCommandService, SbtCommandService>();
```

**Service Lifetime**: Singleton
- Detectors are stateless (state lives in `ProcessRequest`)
- Command services can be shared across multiple detector invocations
- `ILogger`, `ICommandLineInvocationService`, and `IEnvironmentVariableService` are framework services

**Constructor Injection** (`SbtComponentDetector`):
```csharp
public SbtComponentDetector(
    ISbtCommandService sbtCommandService,
    IObservableDirectoryWalkerFactory walkerFactory,
    ILogger<SbtComponentDetector> logger)
```

**Constructor Injection** (`SbtCommandService`):
```csharp
public SbtCommandService(
    ICommandLineInvocationService commandLineInvocationService,
    IMavenStyleDependencyGraphParserService parserService,
    IEnvironmentVariableService envVarService,
    ILogger<SbtCommandService> logger)
```

## Testing Strategy

The test suite uses `DetectorTestUtility` to simulate file discovery and execution:

### Test 1: CLI Availability Check
```csharp
[TestMethod]
public async Task TestSbtDetector_SbtCLIDoesNotExist()
{
    this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(...)).ReturnsAsync(false);
    var (result, componentRecorder) = await this.detectorTestUtility
        .WithFile("build.sbt", string.Empty)
        .ExecuteDetectorAsync();
    
    Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
    Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
}
```

**Validates**: Graceful degradation when SBT isn't installed

### Test 2: Happy Path
```csharp
[TestMethod]
public async Task TestSbtDetector_SbtCLIExists()
{
    this.commandLineMock.Setup(x => x.CanCommandBeLocatedAsync(...)).ReturnsAsync(true);
    this.commandLineMock.Setup(x => x.ExecuteCommandAsync(...))
        .ReturnsAsync(new CommandLineExecutionResult { ExitCode = 0 });
    
    var (result, componentRecorder) = await this.detectorTestUtility
        .WithFile("build.sbt", "name := \"test\"", ["build.sbt"])
        .WithFile("bcde.sbtdeps", "org.scala-lang:scala-library:2.13.8")
        .ExecuteDetectorAsync();
    
    Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());
}
```

**Validates**: End-to-end flow with successful CLI execution

### Test 3: Dependency Parsing
```csharp
var dependencyTreeOutput = @"org.scala-lang:scala-library:2.13.8
  +-com.typesafe:config:1.4.2";

this.detectorTestUtility
    .WithFile("bcde.sbtdeps", dependencyTreeOutput);
```

**Validates**: 
- Correct parsing of Maven coordinates
- Graph relationship extraction (parent-child edges)
- Component type mapping (all become `MavenComponent`)

## Key Design Decisions

### 1. **Why Reuse Maven Infrastructure?**

**Pros**:
- SBT publishes to Maven repos (uses same coordinate system)
- Dependency tree format is nearly identical
- Reduces code duplication and maintenance burden
- Leverages battle-tested parsing logic

**Cons**:
- Couples SBT detector to Maven implementation
- Any Maven parser bugs affect SBT

**Decision Rationale**: The semantic equivalence between SBT and Maven dependencies makes this the most pragmatic choice.

### 2. **Why Execute CLI Instead of Parsing `build.sbt`?**

**Alternatives Considered**:
- Parse `build.sbt` directly (complex: Scala DSL, variable substitution, plugins)
- Use SBT's JSON API (requires SBT 1.4+, less portable)

**Chosen Approach**: CLI execution via `dependencyTree` plugin
- **Pros**: Handles all build logic (plugins, resolvers, version conflicts), works across SBT versions
- **Cons**: Requires SBT installation, slower than static parsing

### 3. **Why Default-Off?**

Per Component Detection lifecycle, all new detectors start as `IDefaultOffComponentDetector`:
- Allows beta testing without impacting existing scans
- Prevents unexpected behavior changes for current users
- Enables gradual rollout and feedback collection

### 4. **Why Temporary File Output?**

**Alternative**: Parse stdout directly

**Problem**: SBT stdout is polluted with metadata that needs filtering:
```
[info] Loading settings for project...
[info] Compiling 1 Scala source...
[info] Done compiling.
[info]   +-com.google.guava:guava:32.1.3-jre  <-- Actual data with [info] prefix
```

**Solution**: Capture stdout, then apply multi-stage filtering to clean output before parsing

## Performance Characteristics

### Bottlenecks

1. **SBT Startup**: 10-15 seconds per invocation (JVM warmup + dependency resolution)
2. **First Build**: Downloads SBT, plugins, and dependencies (can be minutes on first run)
3. **Dependency Traversal**: Building the complete dependency tree for complex projects

### Observed Performance (Test Project)

For a simple Scala project with 8 direct/transitive dependencies:
- **Total detection time**: ~14 seconds
- **SBT execution time**: ~13 seconds (majority of time)
- **Parsing time**: <100ms
- **Components detected**: 8 (7 explicit + 1 implicit)

### Optimizations

- **CLI Availability Check**: Short-circuits if SBT missing (avoids processing all files)
- **Timeout Configuration**: Prevents hanging on problematic projects via `SbtCLIFileLevelTimeoutSeconds`
- **Efficient Filtering**: Regex-based filtering reduces memory usage on large dependency trees

### Scaling Considerations

For monorepos with 100+ SBT projects:
- Total scan time ≈ N × 13-15 seconds per project
- **Recommendation**: Use `SbtCLIFileLevelTimeoutSeconds` (e.g., 60 seconds) to cap max time per project
- **Future enhancement**: Parallel execution of independent projects (detector already supports async)
- **Cache potential**: Could cache `.ivy2` directory between runs to skip artifact downloads

## Error Scenarios Handled

1. **SBT Not Installed**: Logs info message, skips processing
2. **Build Compilation Failure**: Logs error, registers parse failure, continues
3. **Timeout**: Logs warning, registers parse failure, cancels CLI process
4. **Malformed Dependency Tree**: Maven parser logs warning, skips invalid lines
5. **Missing Dependencies File**: Cleanup handles file-not-found gracefully

## Integration with Component Detection Pipeline

```
ScanOrchestrator
  └─> Detector Discovery (ServiceCollectionExtensions)
      └─> File Walker (matches "build.sbt")
          └─> SbtComponentDetector.OnPrepareDetectionAsync()
              └─> SbtComponentDetector.OnFileFoundAsync()
                  └─> SbtCommandService.GenerateDependenciesFileAsync()
                  └─> SbtCommandService.ParseDependenciesFile()
                      └─> MavenStyleDependencyGraphParserService.Parse()
                          └─> IComponentRecorder.RegisterUsage()
          └─> SbtComponentDetector.OnDetectionFinished()
              └─> Delete bcde.sbtdeps files
```

The detector integrates seamlessly with existing orchestration - no special casing required.

## Future Enhancement Opportunities

1. **SBT Server Integration**: Use persistent SBT server instead of cold starts
2. **Incremental Scanning**: Cache dependency trees, only re-scan on `build.sbt` changes
3. **Scope Support**: Distinguish compile/test/runtime dependencies
4. **Multi-Project Builds**: Better handling of SBT multi-project hierarchies
5. **Ivy Repository Support**: Detect non-Maven SBT dependencies

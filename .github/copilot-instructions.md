# Component Detection - AI Coding Agent Instructions

## Project Overview
Component Detection is a **package scanning tool** that detects open-source dependencies across 15+ ecosystems (npm, NuGet, Maven, Go, etc.) and outputs a **dependency graph**. It's designed for build-time scanning and can be used as a library or CLI tool.

## Architecture

### Core Concepts
- **Detectors**: Ecosystem-specific parsers that discover and parse manifest files (e.g., `package.json`, `requirements.txt`)
- **Component Recorders**: Immutable graph stores that track detected components and their relationships
- **Typed Components**: Strongly-typed models for each ecosystem (e.g., `NpmComponent`, `PipComponent`) in `src/Microsoft.ComponentDetection.Contracts/TypedComponent/`

### Project Structure
```
src/
├── Microsoft.ComponentDetection/           # CLI entry point (Program.cs)
├── Microsoft.ComponentDetection.Orchestrator/  # Command execution, DI setup, detector coordination
├── Microsoft.ComponentDetection.Contracts/     # Interfaces (IComponentDetector, IComponentRecorder) and TypedComponent models
├── Microsoft.ComponentDetection.Common/        # Shared utilities (file I/O, Docker, CLI invocation)
└── Microsoft.ComponentDetection.Detectors/     # Per-ecosystem detector implementations (npm/, pip/, nuget/, etc.)
```

### Detector Lifecycle Stages
All new detectors start as **IDefaultOffComponentDetector** (must be explicitly enabled via `DetectorArgs`). Maintainers promote through:
1. **DefaultOff** → 2. **IExperimentalDetector** (enabled but output not captured) → 3. **Default** (fully integrated)

### Dependency Injection
All services auto-register via `ServiceCollectionExtensions.AddComponentDetection()` in Orchestrator. Detectors are discovered at runtime via `[Export]` attribute.

## Creating a New Detector

### Required Steps
1. **Define Component Type** (if new ecosystem):
   - Add enum to `DetectorClass` and `ComponentType` in Contracts
   - Create `YourEcosystemComponent : TypedComponent` with required properties
   - Use `ValidateRequiredInput()` for mandatory fields

2. **Implement Detector**:
   ```csharp
   [Export]
   public class YourDetector : FileComponentDetector, IDefaultOffComponentDetector
   {
       public override string Id => "YourEcosystem";
       public override IEnumerable<string> Categories => [DetectorClass.YourCategory];
       public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.YourType];
       public override IEnumerable<string> SearchPatterns => ["manifest.lock"]; // Glob patterns

       protected override Task OnFileFoundAsync(ProcessRequest request, IDictionary<string, string> detectorArgs)
       {
           var recorder = request.SingleFileComponentRecorder;
           // Parse file, create components, call recorder.RegisterUsage()
       }
   }
   ```

3. **Register Components**:
   ```csharp
   var component = new DetectedComponent(new YourComponent("name", "1.0.0"));
   recorder.RegisterUsage(
       component,
       isExplicitReferencedDependency: true,  // Direct dependency?
       parentComponentId: parentId,           // For graph edges (can be null)
       isDevelopmentDependency: false         // Build-only dependency?
   );
   ```

### Detector Lifecycle Methods
- `OnPrepareDetection()` - **Optional**: Pre-processing (e.g., filter files before parsing)
- `OnFileFoundAsync()` - **Required**: Main parsing logic for matched files
- `OnDetectionFinished()` - **Optional**: Cleanup (e.g., delete temp files)

### Testing Pattern
```csharp
[TestClass]
public class YourDetectorTests : BaseDetectorTest<YourDetector>
{
    [TestMethod]
    public async Task TestBasicDetection()
    {
        var fileContent = "name: pkg\nversion: 1.0.0";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("manifest.lock", fileContent, ["manifest.lock"])
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(1);
    }
}
```

Use minimal file content needed to exercise specific scenarios. Avoid testing multiple features in one test.

### End-to-End Verification
Add test resources to `test/Microsoft.ComponentDetection.VerificationTests/resources/[ecosystem]/` with real-world examples that fully exercise your detector. These run in CI to prevent regressions.

## Development Workflows

### Build & Run
```bash
# Build
dotnet build

# Run scan with new detector (replace YourDetectorId)
dotnet run --project src/Microsoft.ComponentDetection/Microsoft.ComponentDetection.csproj scan \
  --Verbosity Verbose \
  --SourceDirectory /path/to/scan \
  --DetectorArgs YourDetectorId=EnableIfDefaultOff
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test test/Microsoft.ComponentDetection.Detectors.Tests/
```

### Debug Mode
Add `--Debug` flag to wait for debugger attachment on startup (prints PID).

## Key Patterns

### File Discovery
Detectors specify `SearchPatterns` (glob patterns like `*.csproj` or `package-lock.json`). The orchestrator handles file traversal; detectors receive matched files via `ProcessRequest.ComponentStream`.

### Graph Construction
- Use `RegisterUsage()` to add nodes and edges
- `isExplicitReferencedDependency: true` marks direct dependencies (like packages in `package.json`)
- `parentComponentId` creates parent-child edges (omit for flat graphs)
- Some ecosystems don't support graphs (e.g., Go modules) - register components without parents

### Component Immutability
`TypedComponent` classes must be immutable (no setters). Validation happens in constructors via `ValidateRequiredInput()`.

### Directory Exclusion
Detectors can filter directories in `OnPrepareDetection()`. Example: npm detector ignores `node_modules` if a root lockfile exists.

## Common Pitfalls

- **Don't** implement `IComponentDetector` directly unless doing one-shot scanning (like Linux detector). Use `FileComponentDetector` for manifest-based detection.
- **Don't** guess parent relationships - only create edges if the manifest explicitly defines them.
- **Don't** use setters on `TypedComponent` - pass required values to constructor.
- **Always** test with `DetectorTestUtility` pattern, not manual `ComponentRecorder` setup.
- **Remember** new detectors must implement `IDefaultOffComponentDetector` until promoted by maintainers.

## References
- Detector implementation examples: `src/Microsoft.ComponentDetection.Detectors/npm/`, `pip/`, `nuget/`
- Creating detectors: `docs/creating-a-new-detector.md`
- CLI arguments: `docs/detector-arguments.md`
- Test utilities: `test/Microsoft.ComponentDetection.TestsUtilities/DetectorTestUtility.cs`

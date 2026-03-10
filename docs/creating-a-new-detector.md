# Creating a New Detector

## Overview

Component Detection scans a directory (specified by `--SourceDirectory`) to discover open-source dependencies. Detectors are ecosystem-specific parsers that find and process manifest files to build a dependency graph.

## Detector Types

There are two types of detectors:

- **Base detectors** (`IComponentDetector`): Run once per scan, typically for one-shot operations (e.g., Linux container scanning)
- **File-based detectors** (`FileComponentDetector`): Run for each file matching specific patterns (e.g., `package-lock.json`, `requirements.txt`)

This guide focuses on **file-based detectors**, which are the most common type.

## How Detectors Run

1. **Filtering**: Detectors are selected using `--DetectorCategory` (filters by `Categories` property) or `--DetectorFilters` (filters by `Id` property)
2. **File Discovery**: The tool scans for files matching each detector's `SearchPatterns` (glob patterns)
3. **Execution**: Detectors process matched files asynchronously

## Detector Lifecycle Methods

File-based detectors process matched files through three lifecycle methods:

### 1. `OnPrepareDetectionAsync` (Optional)

Runs before file processing begins. Use this for:

- Pre-filtering files (e.g., skip `node_modules` if a root lockfile exists)
- Setting up shared state
- Validating prerequisites

### 2. `OnFileFoundAsync` (Required)

The main parsing logic. Called once per matched file to:

- Parse manifest content
- Extract component information
- Build the dependency graph via `ComponentRecorder`

### 3. `OnDetectionFinishedAsync` (Optional)

Runs after all files are processed. Use this for:

- Cleanup (e.g., deleting temporary files)
- Post-processing aggregations
- Final validation

## Detector Maturity Stages

New detectors progress through three stages before becoming default. Contributors only implement **Stage 1**; maintainers handle promotion through later stages.

| Stage | Interface | Behavior |
|-------|-----------|----------|
| **1. Default Off** | `IDefaultOffComponentDetector` | Must be explicitly enabled via `--DetectorArgs YourDetectorId=EnableIfDefaultOff`. Detector should be fully functional and produce correct output. |
| **2. Experimental** | `IExperimentalDetector` | Runs automatically but output is NOT included in scan results. Used to measure performance impact. |
| **3. Default** | `IComponentDetector` | Fully integrated. Runs by default and output is included in results. Can be filtered via `--DetectorCategory` or `--DetectorFilters`. |

## Step 1: Define Your Component Type

**Skip this step if reusing an existing component type** (e.g., `NpmComponent`, `PipComponent`).

### 1.1 Add a Detector Category

If your ecosystem needs a new category, add it to `DetectorClass` enum in `src/Microsoft.ComponentDetection.Contracts/DetectorClass.cs`:

```csharp
public enum DetectorClass
{
    // ...
    YourEcosystem,
}
```

### 1.2 Add a Component Type

Add your component type to `ComponentType` enum in `src/Microsoft.ComponentDetection.Contracts/TypedComponent/ComponentType.cs`:

```csharp
public enum ComponentType : byte
{
    // ...
    YourType = 20,
}
```

### 1.3 Create a Component Class

Create a new class in `src/Microsoft.ComponentDetection.Contracts/TypedComponent/`:

```csharp
public class YourEcosystemComponent : TypedComponent
{
    private YourEcosystemComponent() { /* Reserved for deserialization */ }

    public YourEcosystemComponent(string name, string version)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.YourType));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.YourType));
    }

    public string Name { get; set; }
    public string Version { get; set; }

    public override ComponentType Type => ComponentType.YourType;
    public override PackageURL PackageUrl => new PackageURL("your-type", null, this.Name, this.Version, null, null);
    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";
}
```

**Key Points:**

- Use `ValidateRequiredInput()` for mandatory properties (throws if null/empty)
- Components must be immutable (properties can have setters for deserialization, but should not be mutated after construction)
- Align property names with the ecosystem's terminology (e.g., Maven uses `GroupId`, `ArtifactId`)

## Step 2: Create the Detector Class

Create a new folder in `src/Microsoft.ComponentDetection.Detectors/` for your detector (e.g., `yourEcosystem/`). Inside this folder, create your detector class:

```csharp
namespace Microsoft.ComponentDetection.Detectors.YourEcosystem;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class YourEcosystemDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public YourEcosystemDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<YourEcosystemDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "YourEcosystem";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.YourEcosystem)];

    public override IList<string> SearchPatterns => ["manifest.lock", "*.yourextension"];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.YourType];

    public override int Version => 1;

    public override bool NeedsAutomaticRootDependencyCalculation => true;

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var recorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogDebug("Processing {FileLocation}", file.Location);

        // Parse file and register components
        // See "Registering Components" section below

        return Task.CompletedTask;
    }
}
```

### Required Properties

- **`Id`**: Unique identifier for your detector (e.g., `"YourEcosystem"`)
- **`Categories`**: Groups detectors by ecosystem (e.g., `["Npm"]` for npm, pnpm, yarn)
- **`SearchPatterns`**: Glob patterns for file discovery (e.g., `["package-lock.json"]`, `["*.csproj"]`)
- **`SupportedComponentTypes`**: Component types this detector creates (e.g., `[ComponentType.Npm]`)
- **`Version`**: Detector version (increment when making breaking changes)
- **`NeedsAutomaticRootDependencyCalculation`**: Set to `true` if you don't explicitly mark root dependencies

### Constructor Requirements

Inject these services via constructor:

- `IComponentStreamEnumerableFactory`: For reading file streams
- `IObservableDirectoryWalkerFactory`: For directory traversal
- `ILogger<T>`: For logging

### Interfaces

- **`FileComponentDetector`**: Base class for all file-based detectors
- **`IDefaultOffComponentDetector`**: **Required for new detectors** - prevents automatic execution until promoted by maintainers

## Step 3: Register in Dependency Injection

Add your detector to `src/Microsoft.ComponentDetection.Orchestrator/Extensions/ServiceCollectionExtensions.cs` in the `AddComponentDetection()` method:

```csharp
public static IServiceCollection AddComponentDetection(this IServiceCollection services)
{
    // ... existing registrations ...

    // YourEcosystem
    services.AddSingleton<IComponentDetector, YourEcosystemDetector>();

    return services;
}
```

This allows the orchestrator to discover and instantiate your detector at runtime.

## Registering Components

Inside `OnFileFoundAsync()`, you'll receive a `ProcessRequest` containing:

- **`ComponentStream`**: The file to parse
  - `Stream`: File contents as a stream
  - `Location`: Full file path
  - `Pattern`: The glob pattern that matched this file

- **`SingleFileComponentRecorder`**: Immutable graph builder for this file

### Using `RegisterUsage()`

Register components in the dependency graph:

```csharp
var component = new DetectedComponent(new YourEcosystemComponent("package-name", "1.0.0"));

recorder.RegisterUsage(
    detectedComponent: component,
    isExplicitReferencedDependency: true,  // Is this a direct/root dependency?
    parentComponentId: parentId,            // Parent component ID (null if root)
    isDevelopmentDependency: false          // Is this a dev/build-only dependency?
);
```

### Parameter Guide

| Parameter | Description | Example |
|-----------|-------------|----------|
| `detectedComponent` | The component to register | `new DetectedComponent(new NpmComponent("react", "18.0.0"))` |
| `isExplicitReferencedDependency` | `true` if user directly referenced this (e.g., in `package.json`), `false` if transitive | Direct deps: `true`, transitive: `false` |
| `parentComponentId` | The parent component's ID to create graph edges. `null` for root components or flat graphs. | `parentComponent.Component.Id` or `null` |
| `isDevelopmentDependency` | `true` if not needed in production (e.g., test frameworks, build tools) | `devDependencies` in npm: `true` |

### Graph Structure Examples

**Flat graph** (no hierarchy):

```csharp
recorder.RegisterUsage(new DetectedComponent(component), isExplicitReferencedDependency: true, parentComponentId: null);
```

**Tree graph** (with parent-child relationships):

```csharp
// Register root
var root = new DetectedComponent(new NpmComponent("app", "1.0.0"));
recorder.RegisterUsage(root, isExplicitReferencedDependency: true);

// Register child
var dep = new DetectedComponent(new NpmComponent("dep", "2.0.0"));
recorder.RegisterUsage(dep, isExplicitReferencedDependency: false, parentComponentId: root.Component.Id);
```

## Step 4: Write Unit Tests

Create tests in `test/Microsoft.ComponentDetection.Detectors.Tests/`. Follow these best practices:

- **One scenario per test**: Each test should validate a single detection scenario
- **Minimal file content**: Use only the necessary sections of a manifest file
- **Use `DetectorTestUtilityBuilder`**: Simplifies test setup

### Example Test Class

```csharp
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.YourEcosystem;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class YourEcosystemDetectorTests : BaseDetectorTest<YourEcosystemDetector>
{
    [TestMethod]
    public async Task TestBasicDetection()
    {
        var fileContent = @"
            name: example-package
            version: 1.0.0
            dependencies:
              dep1: 2.0.0
        ";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("manifest.lock", fileContent, ["manifest.lock"])
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var components = componentRecorder.GetDetectedComponents();
        components.Should().HaveCount(2);

        var rootComponent = components.Single(c => ((YourEcosystemComponent)c.Component).Name == "example-package");
        componentRecorder.GetEffectiveDevDependencyValue(rootComponent.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public async Task TestDevDependencies()
    {
        // Test dev dependency detection
    }
}
```

### Testing Pattern

1. **Inherit from `BaseDetectorTest<T>`**: Provides `DetectorTestUtility` property
2. **Define file content**: Use minimal but realistic manifest content
3. **Build test**: Chain `.WithFile()` calls to add files
4. **Execute**: Call `.ExecuteDetectorAsync()`
5. **Assert**: Verify scan result and component graph

## Running Your Detector

### Build the Project

```bash
dotnet build
```

### Run a Scan

```bash
dotnet run --project src/Microsoft.ComponentDetection/Microsoft.ComponentDetection.csproj scan \
  --Verbosity Verbose \
  --SourceDirectory /path/to/scan \
  --DetectorArgs YourEcosystemDetector=EnableIfDefaultOff
```

**Important**: Since new detectors implement `IDefaultOffComponentDetector`, you **must** use `--DetectorArgs YourDetectorId=EnableIfDefaultOff` to run them. Replace `YourEcosystemDetector` with your detector's `Id` property value.

### Debug Mode

Add `--Debug` flag to wait for debugger attachment:

```bash
dotnet run --project src/Microsoft.ComponentDetection/Microsoft.ComponentDetection.csproj scan \
  --Debug \
  --SourceDirectory /path/to/scan \
  --DetectorArgs YourEcosystemDetector=EnableIfDefaultOff
```

The tool will print a process ID and wait for you to attach a debugger.

## Step 5: Add Verification Tests

Verification tests are end-to-end tests that run the detector against real-world project files. These tests run in CI to prevent regressions.

### Setup

1. Create a directory: `test/Microsoft.ComponentDetection.VerificationTests/resources/yourEcosystem/`
2. Add one or more real project examples that exercise your detector's features
3. Include various scenarios: basic dependencies, dev dependencies, nested structures, edge cases

### Example Structure

```text
test/Microsoft.ComponentDetection.VerificationTests/resources/
├── npm/
│   ├── simple-app/
│   │   ├── package.json
│   │   └── package-lock.json
│   └── monorepo/
│       └── ...
└── yourEcosystem/
    ├── basic-project/
    │   └── manifest.lock
    └── complex-project/
        └── manifest.lock
```

These tests automatically verify that your detector:

- Produces consistent results across multiple runs
- Correctly parses real-world manifest files
- Maintains backward compatibility

## Summary Checklist

Before submitting your detector:

- [ ] Component type defined (if new ecosystem) in `ComponentType.cs` and `DetectorClass.cs`
- [ ] Component class created in `TypedComponent/` folder with proper validation
- [ ] Detector class created inheriting from `FileComponentDetector` and implementing `IDefaultOffComponentDetector`
- [ ] Detector registered in `ServiceCollectionExtensions.cs`
- [ ] Constructor properly injects required services
- [ ] `SearchPatterns` defined for file discovery
- [ ] `OnFileFoundAsync()` implemented with component registration logic
- [ ] Unit tests created in `Detectors.Tests/` using `DetectorTestUtilityBuilder`
- [ ] Verification test resources added to `VerificationTests/resources/`
- [ ] Detector tested locally with `--DetectorArgs YourDetectorId=EnableIfDefaultOff`

## Additional Resources

- [Detector Arguments](./detector-arguments.md) - Available command-line arguments
- [Feature Overview](./feature-overview.md) - Component Detection capabilities
- [Enable Default Off Detectors](./enable-default-off.md) - How to enable experimental detectors
- Example detectors: `npm/`, `pip/`, `nuget/` in `src/Microsoft.ComponentDetection.Detectors/`

# Ruby

## Requirements

The Ruby detector scans for Ruby dependencies defined in Bundler lockfiles.

**File Patterns:** `Gemfile.lock`

**Supported Ecosystems:** RubyGems

## Detection Strategy

The detector parses `Gemfile.lock` files to identify Ruby gems and their dependencies. It processes the lockfile in multiple passes:

### Parsing Approach

1. **Section-based parsing**: The detector reads the lockfile by sections, which are identified by all-caps headings (`GEM`, `GIT`, `PATH`, `BUNDLED WITH`, etc.)

2. **Component registration**: For each section, the detector extracts:
   - **GEM section**: Standard RubyGems components with name, version, and remote source
   - **GIT section**: Git-based dependencies with remote URL and revision
   - **PATH section**: Local path dependencies
   - **BUNDLED WITH section**: The Bundler version used to generate the lockfile

3. **Dependency graph construction**: After collecting all components, the detector creates parent-child relationships by:
   - Identifying top-level dependencies (4-space indentation)
   - Mapping sub-dependencies (6-space indentation) to their parent components
   - Using automatic root dependency calculation to determine direct vs transitive dependencies

### Component Types

- **RubyGemsComponent**: Standard gems from RubyGems.org or custom sources
  - Properties: name, version, source
- **GitComponent**: Git-based dependencies
  - Properties: remote URL, revision

## Known Limitations

### Version Resolution Constraints

- **Relative versions are excluded**: Components with relative version specifiers (starting with `~` or `=`) are skipped and logged as parse failures. Only absolute versions are registered.
- **Fuzzy version handling**: Different sections of the lockfile can reference the same component, but authoritative version information is only stored in specific sections (e.g., the GEM section), requiring cross-section resolution.

### Git Component Naming

- Git components use a Ruby-specific "name" annotation that doesn't map directly to standard GitComponent semantics (remote/version). The detector works around this by maintaining a name-to-component mapping during parsing.

### Root Dependency Detection

- The detector uses **automatic root dependency calculation** rather than parsing the `DEPENDENCIES` section of `Gemfile.lock` (which lists user-specified dependencies from the `Gemfile`).
- This approach may not perfectly distinguish between direct and transitive dependencies in all cases.

### Bundler Source Information

- The `bundler` version is always registered with `"unknown"` as its source, since the lockfile doesn't specify where Bundler originated.

### Excluded Dependencies

- When a parent component has a relative version and is excluded, all of its child dependencies are also excluded from the dependency graph to maintain consistency.

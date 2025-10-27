## Overview

The **RustSbomDetector** detects components in Rust projects that use Cargo as their build system.  
It identifies all Cargo packages (crates) referenced within a repository, including both workspace-owned and external dependencies, and reports them under the `cargo` component type in the scan manifest.

The detector’s implementation resides in `RustSbomDetector`.

---

## SBOM Mode

When Cargo-generated SBOMs are present, they serve as the **authoritative source of dependency information**.  
SBOMs are produced by running Cargo with the unstable [SBOM feature](https://doc.rust-lang.org/cargo/reference/unstable.html#sbom), which emits a machine-readable dependency graph per build artifact.

In this mode:

- Every discovered `*.cargo-sbom.json` is parsed individually; **no skip logic** is applied.  
- Each SBOM corresponds to a specific build artifact or package target, so all are processed to ensure complete coverage.  
- Dependencies, including transitive ones, are read directly from SBOM entries.  
- SBOMs are considered the most accurate reflection of the built dependency graph, with minimal risk of false positives or duplicate detections.

---

## Fallback Mode

If no Cargo SBOMs are found, the detector switches to **Fallback Mode**, which derives dependency information from the project’s manifests and lock files:

1. The detector attempts to run `cargo metadata` on each discovered `Cargo.toml`.  
   - This produces a structured dependency graph for that manifest and its workspace members.  
2. If the CLI is unavailable or fails, the detector falls back to parsing the corresponding `Cargo.lock` files directly.

Because both `cargo metadata` and `Cargo.lock` describe potentially broader dependency sets than what is actually built, this mode can occasionally **over-report dependencies**, resulting in potential false positives compared to SBOM Mode.

---

### Skip Optimizations (Applied in Fallback Mode)

To improve performance in multi-package repositories, the detector avoids redundant processing by applying **skip logic**:

- **Child TOMLs are skipped** if a parent `Cargo.toml` has already been processed and its `cargo metadata` output includes the child as a workspace member.  
- **Lock files are skipped** if a `Cargo.toml` in the same directory has already been processed (since running `cargo metadata` produces equivalent results).  

These optimizations potentially reduce the number of redundant `cargo metadata` invocations without affecting detection completeness.

---

## Mapping Components to Locations

### Why

Cargo SBOM files (`*.cargo-sbom.json`) describe dependencies as **build artifacts**, not as user-authored source definitions.  
While they accurately represent what was built, they are not actionable for repository owners — fixing or updating a dependency typically happens by editing a `Cargo.toml`, not the generated SBOM.

For this reason, Component Detection maps each detected dependency back to the **`Cargo.toml` file that introduced it**, ensuring that scan results point developers to the source manifest responsible for including the dependency.

---

### Implementation

To achieve accurate attribution between dependencies and their source manifests, the detector constructs a mapping between **packages** and their **owning `Cargo.toml` files**.  
This process differs slightly between modes:

| Mode | Mapping Source | Behavior |
|------|----------------|-----------|
| **SBOM Mode** | `cargo metadata` run on discovered TOMLs | Used to correlate each package from the SBOM with the manifest that owns it. |
| **SBOM Mode (CLI unavailable / failed)** | SBOM file itself | Packages are mapped to their corresponding `*.cargo-sbom.json` file as a fallback. |
| **Fallback Mode** | `cargo metadata` or lock file parsing | Packages are mapped directly to the TOML or LOCK file that defines or references them. |

In all cases, the goal is to surface the **most relevant and editable source location** for each dependency.  
This mapping enables future integrations — such as pull request comments or IDE feedback — to guide developers directly to the file where a dependency can be modified.

---

## `DisableRustCliScan` Environment Variable

The detector supports an optional environment variable named **`DisableRustCliScan`**, which controls whether the Cargo CLI (`cargo metadata`) is invoked during detection.

When this variable is set to `true`, the detector **skips all Cargo CLI execution**, including metadata queries.  
As a result:

- In **SBOM Mode**, the detector will not run `cargo metadata` to build ownership mappings.  
  - Dependencies will instead be mapped directly to the corresponding `*.cargo-sbom.json` files.  
- In **Fallback Mode**, the detector cannot rely on CLI-generated dependency graphs.  
  - It falls back to processing `Cargo.lock` and `Cargo.toml` files to infer dependencies and relationships.

Because Cargo lock files and manifest parsing provide less contextual information than `cargo metadata`, disabling the CLI may reduce the precision of component-to-location mapping and can lead to **over-reporting**.

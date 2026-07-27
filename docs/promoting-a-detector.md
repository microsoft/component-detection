# Promoting a Detector

This guide is for **maintainers** who need to promote a detector through its lifecycle stages.

For creating a new detector, see [Creating a New Detector](./creating-a-new-detector.md).

When a new detector is merged, create a [Detector Promotion Tracker](https://github.com/microsoft/component-detection/issues/new?template=detector_promotion.yml) issue to track its progression through each stage.

## Detector Lifecycle Overview

Every new detector starts as **Default Off** and progresses through three stages before becoming fully integrated:

```
┌──────────────┐     ┌──────────────────┐     ┌─────────────┐
│  Default Off │ ──► │   Experimental   │ ──► │   Default   │
│   (Stage 1)  │     │    (Stage 2)     │     │  (Stage 3)  │
└──────────────┘     └──────────────────┘     └─────────────┘
  Contributors         Maintainers Only       Maintainers only
```

| Stage | Interface | Behavior | Purpose |
|-------|-----------|----------|---------|
| **Default Off** | `IDefaultOffComponentDetector` | Does not run automatically, but when enabled is treated as a normal, default detector | Following contribution, allows execution of detector on demand by customers. 
| **Experimental** | `IExperimentalDetector` | Runs automatically, with a shorter timeout, but all results discarded unless explicitely enabled | Interim step before default enablement, allows performance and result comparisons and validation
| **Default** | `IComponentDetector` | Runs by default on all executions, all results always submitted. | Final state, expectation is that this detector has been vetted and all results are accurate. 

# Promotion Stages
If one is not created yet, create a [Detector Promotion Tracker](https://github.com/microsoft/component-detection/issues/new?template=detector_promotion.yml) issue to track its progression through each stage.

## Stage 1 → Stage 2: Default Off → Experimental

### When to Promote

A detector is ready for Experimental when:

- All unit tests pass reliably
- The detector has been manually tested against real-world projects (real work projects provided in issue)
- Edge cases are handled (empty files, malformed input, missing fields)
- No performance regressions observed when running locally
- The detector was reviewed and approved by at least one maintainer

### Necessary items to promote
- Verification test resources are checked in
- Determine if an Experiment Design is necessary:
    - A detector is producing results that are comparable to a different detector
    - Experiment queries should be constructed, outlining expections, and markers for success/failure created. 

### How to Promote

**1. Change the interface** on the detector class declaration:

```diff
- public class YourDetector : FileComponentDetector, IDefaultOffComponentDetector
+ public class YourDetector : FileComponentDetector, IExperimentalDetector
```

**2. Update the detector status table** in [`docs/detectors/README.md`](./detectors/README.md):

Change the detector's status from `DefaultOff` to `Experimental`.

**3. Experiment Design**:1


### What Happens After Promotion

Once merged, the detector will:

- **Run automatically** on every scan (no `--DetectorArgs` needed)
- **Not affect scan results** — detected components are discarded after execution
- **Be capped at 4 minutes** — if the detector exceeds this, it is cancelled
- **Emit telemetry** — the `IsExperimental` flag is set on telemetry records, allowing you to monitor performance, component counts, and error rates in production

### What to Monitor

During the Experimental phase, watch for:

- **Execution time**: Is the detector completing within the 4-minute cap? Check telemetry for timeout cancellations.
- **Error rate**: Are there frequent parsing failures or exceptions? Check logs for warnings.
- **Component counts**: Are the numbers reasonable compared to expectations? Unusually high counts may indicate false positives.
- **Memory usage**: Does the detector cause memory pressure on large repositories?

## Stage 2 → Stage 3: Experimental → Default

### When to Promote

A detector is ready for Default when:

- It has been running as Experimental in production for a sufficient period (typically 2+ weeks)
- Telemetry shows stable performance — no timeouts, low error rate
- Component detection accuracy has been validated against known repositories
- No regressions reported from Experimental telemetry
- Verification test snapshots are up to date and passing
- Documentation is complete ([`docs/detectors/`](./detectors/) has a page for the detector)
- The detector version has been bumped if any output changes were made during Experimental

### How to Promote

**1. Remove the marker interface** from the detector class declaration:

```diff
- public class YourDetector : FileComponentDetector, IExperimentalDetector
+ public class YourDetector : FileComponentDetector
```

Since `FileComponentDetector` already implements `IComponentDetector`, no replacement interface is needed.

**2. Update the detector status table** in [`docs/detectors/README.md`](./detectors/README.md):

Change the detector's status from `Experimental` to `Stable`.

**3. Update the Feature Overview** if applicable — add the detector to the main [README.md](../README.md) feature table if it represents a new ecosystem.

**4. Bump the detector version** if any output changes were made

### What Happens After Promotion

Once merged, the detector will:

- **Run automatically** on every scan
- **Include components in scan results** — detected components are now part of the dependency graph output
- **Use the normal timeout** (no longer capped at 4 minutes)
- **Affect downstream consumers** — Component Governance, SBOM generation, and vulnerability scanning will now include these components

> **⚠️ This is an irreversible user-facing change.** Once users depend on components from this detector appearing in scan results, removing them is a breaking change. Make sure the detector is production-ready.

## Rollback: Demoting a Detector

If a detector needs to be pulled back after promotion:

### Default → Experimental (hide results, keep running)

```diff
- public class YourDetector : FileComponentDetector
+ public class YourDetector : FileComponentDetector, IExperimentalDetector
```

### Default/Experimental → Default Off (stop running)

```diff
- public class YourDetector : FileComponentDetector, IExperimentalDetector
+ public class YourDetector : FileComponentDetector, IDefaultOffComponentDetector
```

Update `docs/detectors/README.md` status accordingly.

## Quick Reference

### The one-line change for each promotion

| Promotion | Change |
|-----------|--------|
| DefaultOff → Experimental | Replace `IDefaultOffComponentDetector` with `IExperimentalDetector` |
| Experimental → Default | Remove `IExperimentalDetector` (no replacement needed) |
| Default → Experimental | Add `IExperimentalDetector` |
| Any → DefaultOff | Replace/add `IDefaultOffComponentDetector` |

### Files to update for any promotion

1. **Detector class** — change the interface (required)
2. **`docs/detectors/README.md`** — update status table (required)
3. **Detector `Version` property** — bump if output changed (if applicable)
4. **Root `README.md`** — update feature table (Stage 3 promotions only, if new ecosystem)

## See Also

- [Creating a New Detector](./creating-a-new-detector.md) — Full guide for contributors
- [Enable Default Off Detectors](./enable-default-off.md) — How to test DefaultOff detectors locally
- [Detector Documentation](./detectors/README.md) — Status table for all detectors

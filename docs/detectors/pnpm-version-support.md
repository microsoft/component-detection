# pnpm v10-v12 detector support tracker

**Status:** Initial research recorded November 18, 2026. Registry-qualified version handling is implemented; broader compatibility fixtures are pending.

**Goal:** Detect resolved npm dependencies from pnpm v10, v11, and v12 standalone projects and workspaces that use lockfile schema `9.0`, including lockfile-recorded config and package-manager dependencies. The opt-in `13.0` schema is out of scope. Preserve existing lockfile v5, v6, and v9 behavior. This is a static scan: do not run pnpm, execute hooks, or resolve missing versions against a registry.

## Current baseline

The [Pnpm detector](pnpm.md) dispatches on **lockfile schema**, not the pnpm CLI major. It recognizes schemas 5, 6, and 9 and searches `pnpm-lock.yaml` and legacy `shrinkwrap.yaml`. It does not read `pnpm-workspace.yaml` directly; workspace detection uses the resolved projects in the lockfile's `importers`.

The [schema 9 parser](../../src/Microsoft.ComponentDetection.Detectors/pnpm/Pnpm9Detector.cs) builds the graph from `importers` and `snapshots`, including the two-document layout when both documents declare `9.0`. It reads optional roots, but the shared [snapshot model](../../src/Microsoft.ComponentDetection.Detectors/pnpm/Contracts/Package.cs) has no `optionalDependencies` edges and expects `resolution` values to be strings. The [version reader](../../src/Microsoft.ComponentDetection.Detectors/pnpm/ParsingUtilities/PnpmParsingUtilitiesFactory.cs) rejects different version strings across documents before graph parsing. Existing multi-document tests use synthetic examples; recognizing a schema number is not evidence that every CLI release or feature works.

## pnpm v10: schema 9.0 with new dependency forms

The [v10 lockfile writer][v10-writer] still emits [`9.0`][v10-schema] with `importers`, `packages`, and `snapshots`. Keep the schema 9 parser and test the newer entries that can appear inside it.

| Change | Detector impact |
| --- | --- |
| [v10.0][v10-release] switches long peer suffixes and patch/checksum fields to SHA-256, manages the pinned pnpm version by default, and blocks dependency lifecycle scripts by default. | Match full snapshot keys without assuming a hash algorithm. Keep peer and patch context for edges without making it part of the npm component version. Build approval and package-manager switching do not require re-running install logic. |
| v10.0 changes workspace linking and requires injected workspace packages for the new `deploy` flow. It also drops automatic conversion from lockfile v6. | Cover external dependencies reached through injected workspace packages; preserve the scanner's independent v6 reader. |
| [Config dependencies][v10-config] are pinned as `version+integrity` in `pnpm-workspace.yaml`. | They can be absent from `pnpm-lock.yaml` and are invisible to the current detector. Decide whether to add a narrow manifest reader for these exact pins; do not treat arbitrary catalog ranges or overrides as installed packages. |
| [v10.9 adds `jsr:`][v10-year]; [v10.12.1 adds catalog modes][v10-catalogs]. | Verify how JSR and npm aliases resolve in the lockfile. Use the actual locked target for `catalog:` dependencies, not the alias or the catalog range. |
| [v10.14][v10-runtime] locks Node.js, Deno, and Bun from `devEngines.runtime`; [v10.21][v10-transitive-runtime] also installs Node.js for dependencies' `engines.runtime`. | Test runtime keys and structured resolutions alongside ordinary npm entries. A runtime is not an npm package solely because it appears in the npm lockfile. |

## pnpm v11: schema 9.0 with an environment document

- The [v11.0.0 constant][v11-schema] remains `9.0`. The [release notes][v11-release] move config-dependency resolutions and integrity out of `pnpm-workspace.yaml` into an optional first YAML document. It also contains resolved pnpm pins (`packageManagerDependencies`), including versions selected from a `devEngines.packageManager` range. The last document contains the project graph. The [v11 writer][v11-env] initializes the environment document with `9.0`; [upstream compatibility tests][v11-env-tests] also use the nonnumeric `env-1.0` marker. Treat that marker as a fixture candidate for a recognized environment shape, not the default v11 output.
- The [v11.0 release][v11-release] moves general project settings into `pnpm-workspace.yaml`, adds scoped `registries` and per-project `packageConfigs`, replaces the old build-approval settings with `allowBuilds`, and introduces `dedupePeers`, which can shorten nested peer suffixes. [v11.2][v11-optional] includes one level of config-package optional dependencies and records all platform variants. Test both peer forms and optional edges; do not simulate settings, registry access, or build policies.
- [Named registry support][v11-sources] adds dependency aliases in v11.1, and [v11.20][registry-keys] uses registry-qualified keys (`name@registry:version`). [v11.25][v11-revisions] adds registry revisions, and [v11.26][v11-workspace-catalogs] allows `workspace:` ranges in catalogs. Preserve source distinctions for graph lookup and verify component identity separately.
- [Upstream runtime examples][runtime-shapes] record entries like `node@runtime:22.0.0` and can nest platform variants under `resolution`. The current string-only `resolution` map cannot represent that shape, even though the graph does not use it to identify ordinary packages. Relax or model resolution metadata so a runtime entry cannot hide the rest of the npm graph.

## pnpm v12: schema 9.0

[pnpm v12.0][v12-release] switches the CLI to Rust but [retains schema 9.0][v12-schema]. Treat it as another writer to test, not a reason to route on the CLI major. Upstream's [release listing][releases] shows `12.22.0` as the current v12 release as of this research.

| Change | Detector impact |
| --- | --- |
| v12.0 adds registry revisions (`resolution.revision`, with `+rN` pins) and can manage other package managers and runtimes. [v12.13][package-sources] adds `tool:` but still writes `runtime:` in the lockfile. [v12.15][v12-includes] adds `lockfile.integrityTarget: tar` and tar registries. | Accept the additional resolution shapes. Verify that revision and integrity metadata do not change npm identity accidentally, and that native tools are not mislabeled as registry npm packages. |
| [v12.2-v12.3][v12-workspace-catalogs] adds `workspace:` support inside catalogs; [v12.6][v12-local-catalogs] adds `file:` and `link:`. [v12.7][v12-workspaces] can create `pnpm-workspace.yaml` from `package.json` workspace globs. [v12.12][v12-config] supports saving config-dependency ranges, and v12.15 can delegate named settings via `include`. | Test locked external dependencies of linked and injected projects, and use the exact environment resolution for config dependencies. Do not follow includes or infer dependencies from workspace globs when the lockfile already supplies the graph. |
| [v12.4][v12-polyglot] manages Cargo and Python in the same workspace but retains their separate `Cargo.lock` and `pylock.toml`. | Keep ecosystem attribution separate. Track any missing Cargo/Python coverage with the respective detectors. |
| [v12.16][v12-registry-keys] consistently uses registry-qualified keys for a registry with a prefix. [v12.18][v12-variants] adds platform variants for npm packages served by pnpr, not just for runtime binaries. | Keep registry-qualified graph keys, export the bare semantic version, and test nested variant resolutions separately. |

[v12.19][v12-next] also introduced experimental `lockfile.format: v13`. That format remains unsupported.

## Implementation and verification

- [x] Compare release notes and tagged upstream writers with the local dispatch, graph model, and existing tests.
- [x] Fix the v12 schema 9 regression where a named-registry key such as `sample@my-registry:0.1.12` exported `my-registry:0.1.12` instead of `0.1.12`, which Governance rejected. Retain the full key for direct and transitive lookups and preserve pnpm protocol prefixes that are not [registry aliases][registry-aliases].
- [ ] Generate minimal standalone and multi-project fixtures, starting with pnpm `10.31.0`, `11.0.0`, `11.28.13`, `12.0.0`, and `12.22.0`. Record the producer, settings, and expected graph with each fixture; commit the resulting inputs so detector tests can run offline. Include shared and per-project locks (`sharedWorkspaceLockfile: false`).
- [ ] Extend the existing schema 9 parser where fixtures show gaps: nested resolution metadata, optional transitive edges, aliases, and peer/patch-aware lookups. Cover the v10 manifest-only config pin if in scope. Keep ordinary npm component IDs and direct/dev attribution stable.
- [ ] Make document selection role-aware: parse the project version from the last document, validate and read a recognized environment document separately, and register config and manager dependencies without joining unrelated snapshot edges across documents. Verify whether to support an environment-only file: the upstream writer can leave the project document empty before the first full install. Reject extra documents and arbitrary mixed schemas.
- [ ] Add targeted graph assertions to [PnpmDetectorTests](../../test/Microsoft.ComponentDetection.Detectors.Tests/PnpmDetectorTests.cs) and end-to-end inputs under [verification resources](../../test/Microsoft.ComponentDetection.VerificationTests/resources/pnpm). Assert identities, direct/transitive and optional edges, production winning over dev when both are reachable, and no loss of ordinary npm packages in mixed inputs. Retain v5/v6/v9 regressions and bump the `Pnpm` detector result version when output changes.

**Next implementation slice:** Verify schema 9 compatibility on v10, v11, and default v12 output. Track workspace-only declarations, alternate lockfile paths such as branch lockfiles, and inventory of non-npm tools separately unless the fixture work makes them essential.

[v10-release]: https://github.com/pnpm/pnpm/releases/tag/v10.0.0
[v10-writer]: https://github.com/pnpm/pnpm/blob/v10.31.0/lockfile/fs/src/lockfileFormatConverters.ts
[v10-schema]: https://github.com/pnpm/pnpm/blob/v10.31.0/packages/constants/src/index.ts
[v10-config]: https://pnpm.io/10.x/config-dependencies
[v10-year]: https://pnpm.io/blog/2025/12/29/pnpm-in-2025
[v10-catalogs]: https://github.com/pnpm/pnpm/releases/tag/v10.12.1
[v10-runtime]: https://pnpm.io/blog/releases/10.14
[v10-transitive-runtime]: https://pnpm.io/blog/releases/10.21
[v11-release]: https://pnpm.io/blog/releases/11.0
[v11-schema]: https://github.com/pnpm/pnpm/blob/v11.0.0/core/constants/src/index.ts
[v11-env]: https://github.com/pnpm/pnpm/blob/v11.0.0/lockfile/fs/src/envLockfile.ts
[v11-env-tests]: https://github.com/pnpm/pnpm/blob/v11.0.0/lockfile/fs/test/write.test.ts
[v11-optional]: https://pnpm.io/blog/releases/11.2
[v11-sources]: https://pnpm.io/blog/releases/11.1
[registry-keys]: https://pnpm.io/blog/releases/11.20
[v11-revisions]: https://pnpm.io/blog/releases/11.25
[v11-workspace-catalogs]: https://pnpm.io/blog/releases/11.26
[runtime-shapes]: https://github.com/pnpm/pacquet/issues/437
[v12-release]: https://github.com/pnpm/pnpm/releases/tag/v12.0.0
[v12-schema]: https://github.com/pnpm/pnpm/blob/v12.0.0/pnpm/crates/lockfile/src/lib.rs
[releases]: https://github.com/pnpm/pnpm/releases
[v12-workspace-catalogs]: https://pnpm.io/blog/releases/12.2-12.3
[v12-polyglot]: https://pnpm.io/blog/releases/12.4
[v12-local-catalogs]: https://pnpm.io/blog/releases/12.6
[v12-workspaces]: https://pnpm.io/blog/releases/12.7
[v12-config]: https://pnpm.io/blog/releases/12.12.0
[package-sources]: https://pnpm.io/package-sources
[v12-includes]: https://pnpm.io/blog/releases/12.15.0
[v12-registry-keys]: https://pnpm.io/blog/releases/12.16.0
[v12-variants]: https://pnpm.io/blog/releases/12.18.0
[v12-next]: https://pnpm.io/blog/releases/12.19.0
[registry-aliases]: https://github.com/pnpm/pnpm/blob/v12.22.0/pnpm/crates/deps-path/src/registry_qualified.rs

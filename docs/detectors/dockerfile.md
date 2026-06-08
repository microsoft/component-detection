# Dockerfile Detection

## Requirements

Dockerfile detection depends on the following to successfully run:

- One or more Dockerfile files matching the patterns: `dockerfile`, `dockerfile.*`, or `*.dockerfile`

The `DockerfileComponentDetector` is an **Experimental** detector. It runs automatically during scans, but its output is not included in the final scan results. To include its output, pass `--DetectorArgs DockerReference=Enable` (the key is the detector Id `DockerReference`, not the class name).

## Detection strategy

The Dockerfile detector parses Dockerfile syntax to extract Docker image references from `FROM` and `COPY --from` instructions. It uses the [Valleysoft.DockerfileModel](https://github.com/mthalman/DockerfileModel) library to parse Dockerfile syntax.

### FROM Instruction Detection
The detector extracts base image references from `FROM` instructions and resolves multi-stage build references:
- Direct image references (e.g., `FROM ubuntu:22.04`)
- Multi-stage builds with stage names (e.g., `FROM node:18 AS builder`)
- Stage-to-stage references are tracked to avoid reporting internal build stages as external dependencies

### COPY --from Instruction Detection
The detector extracts image references from `COPY --from=<image>` instructions that reference external images rather than build stages.

### Variable Resolution
The detector attempts to resolve Dockerfile variables using the `ResolveVariables()` method from the parser library. Images with unresolved variables (containing `$`, `{`, or `}` characters) are skipped to avoid reporting incomplete or incorrect references.

### Tag and Digest Support
The detector supports the full Docker reference grammar via `DockerReferenceUtility.ParseFamiliarName()`. Image references are parsed and reported with their tag, digest, or both:
- Tagged references (e.g., `FROM nginx:1.21`) populate the `Tag` field
- Canonical references with a SHA256 digest (e.g., `FROM nginx@sha256:abc...`) populate the `Digest` field
- Dual references with both a tag and a digest (e.g., `FROM nginx:1.21@sha256:abc...`) populate both fields

## Known limitations

- **Experimental Status**: This detector runs automatically but its output is not included in scan results by default. To opt in, pass `--DetectorArgs DockerReference=Enable`
- **Variable Resolution**: Image references containing unresolved Dockerfile `ARG` or `ENV` variables are not reported, which may lead to under-reporting in Dockerfiles that heavily use build-time variables
- **No Version Pinning Validation**: The detector does not warn about unpinned image versions (e.g., `latest` tags), which are generally discouraged in production Dockerfiles
- **Untagged Images Skipped**: Image references with neither a tag nor a digest (e.g. `FROM nginx`) are skipped because they cannot be uniquely identified

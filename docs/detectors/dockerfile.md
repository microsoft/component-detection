# Dockerfile Detection

## Requirements

Dockerfile detection depends on the following to successfully run:

- One or more Dockerfile files matching the patterns: `dockerfile`, `dockerfile.*`, or `*.dockerfile`

The `DockerfileComponentDetector` is a **DefaultOff** detector and must be explicitly enabled via the `--DetectorArgs` parameter.

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

## Known limitations

- **DefaultOff Status**: This detector must be explicitly enabled using `--DetectorArgs DockerReference=EnableIfDefaultOff`
- **Variable Resolution**: Image references containing unresolved Dockerfile `ARG` or `ENV` variables are not reported, which may lead to under-reporting in Dockerfiles that heavily use build-time variables
- **No Version Pinning Validation**: The detector does not warn about unpinned image versions (e.g., `latest` tags), which are generally discouraged in production Dockerfiles
- **No Digest Support**: While Docker supports content-addressable image references using SHA256 digests (e.g., `ubuntu@sha256:abc...`), the parsing and reporting of these references depends on the underlying `DockerReferenceUtility.ParseFamiliarName()` implementation

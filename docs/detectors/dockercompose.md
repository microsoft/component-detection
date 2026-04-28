# Docker Compose Detection

## Requirements

Docker Compose detection depends on the following to successfully run:

- One or more Docker Compose files matching the patterns: `docker-compose.yml`, `docker-compose.yaml`, `docker-compose.*.yml`, `docker-compose.*.yaml`, `compose.yml`, `compose.yaml`, `compose.*.yml`, `compose.*.yaml`

The `DockerComposeComponentDetector` is a **DefaultOff** detector and must be explicitly enabled via the `--DetectorArgs` parameter.

## Detection strategy

The Docker Compose detector parses YAML compose files to extract Docker image references from service definitions.

### Service Image Detection

The detector looks for the `services` section and extracts the `image` field from each service:

```yaml
services:
  web:
    image: nginx:1.21
  db:
    image: postgres:14
```

Services that only define a `build` directive without an `image` field are skipped, as they do not reference external Docker images.

### Full Registry References

The detector supports full registry image references:

```yaml
services:
  app:
    image: ghcr.io/myorg/myapp:v2.0
```

### Variable Resolution

Images containing unresolved variables (e.g., `${TAG}` or `${REGISTRY:-docker.io}`) are skipped to avoid reporting incomplete or incorrect references. The detector checks for `$`, `{`, or `}` characters in image references.

## Known limitations

- **DefaultOff Status**: This detector must be explicitly enabled using `--DetectorArgs DockerCompose=EnableIfDefaultOff`
- **Variable Resolution**: Image references containing unresolved environment variables or template expressions are not reported, which may lead to under-reporting in compose files that heavily use variable substitution
- **Build-Only Services**: Services that only specify a `build` directive without an `image` field are not reported
- **No Dependency Graph**: All detected images are registered as independent components without parent-child relationships
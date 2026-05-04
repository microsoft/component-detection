# Docker Compose Detection

## Requirements

Docker Compose detection depends on the following to successfully run:

- One or more Docker Compose files matching the patterns: `docker-compose.yml`, `docker-compose.yaml`, `docker-compose.*.yml`, `docker-compose.*.yaml`, `compose.yml`, `compose.yaml`, `compose.*.yml`, `compose.*.yaml`

The `DockerComposeComponentDetector` is an **Experimental** detector. It runs automatically during scans, but its output is not included in the final scan results. To include its output, pass `--DetectorArgs DockerCompose=Enable` (the key is the detector Id `DockerCompose`, not the class name).

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

- **Experimental Status**: This detector runs automatically but its output is not included in scan results by default. To opt in, pass `--DetectorArgs DockerCompose=Enable`
- **Variable Resolution**: Image references containing unresolved environment variables or template expressions are not reported, which may lead to under-reporting in compose files that heavily use variable substitution
- **Build-Only Services**: Services that only specify a `build` directive without an `image` field are not reported
- **No Dependency Graph**: All detected images are registered as independent components without parent-child relationships
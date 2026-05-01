# Helm Detection

## Requirements

Helm detection depends on the following to successfully run:

- One or more Helm values files matching the patterns: `*values*.yaml`, `*values*.yml`
- A chart metadata file named `Chart.yaml` or `Chart.yml` must exist in the same directory for file discovery/co-location checks; only values files are parsed for image references
  - Lowercase `chart.yaml` and `chart.yml` do not satisfy this requirement; the detector requires an uppercase `Chart.*` file name.

The `HelmComponentDetector` is an **Experimental** detector. It runs automatically during scans, but its output is not included in the final scan results. To include its output, pass `--DetectorArgs Helm=Enable` (the key is the detector Id `Helm`, not the class name).

## Detection strategy

The Helm detector parses Helm values YAML files to extract Docker image references. It recursively walks the YAML tree looking for `image` keys.

### Direct Image Strings

The detector recognizes image references specified as simple strings:

```yaml
image: nginx:1.21
```

### Structured Image Objects

The detector also supports the common Helm chart pattern of structured image definitions:

```yaml
image:
  registry: ghcr.io
  repository: org/myimage
  tag: v1.0
```

The `registry` and `tag` fields are optional. When present, the detector reconstructs the full image reference. The `digest` field is also supported.

### Recursive Search

The detector recursively traverses all nested mappings and sequences in the values file, detecting image references at any depth in the YAML structure.

### Variable Resolution

Images containing unresolved variables (e.g., `{{ .Values.tag }}`) are skipped to avoid reporting incomplete or incorrect references. The detector checks for `$`, `{`, or `}` characters in image references.

## Known limitations

- **Experimental Status**: This detector runs automatically but its output is not included in scan results by default. To opt in, pass `--DetectorArgs Helm=Enable`
- **Values Files Only**: Only files with `values` in the name are parsed for image references. Chart.yaml files are matched but not processed
- **Same-Directory Co-location**: Values files are only processed when a `Chart.yaml` (or `Chart.yml`) exists in the **same directory**. Values files in subdirectories of a chart root (e.g., `mychart/subdir/values.yaml`) will not be detected, even if a `Chart.yaml` exists in the parent directory
- **Variable Resolution**: Image references containing unresolved Helm template expressions are not reported
- **No Dependency Graph**: All detected images are registered as independent components without parent-child relationships
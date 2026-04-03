# Kubernetes Detection

## Requirements

Kubernetes detection depends on the following to successfully run:

- One or more Kubernetes manifest files matching the patterns: `*.yaml`, `*.yml`
- Manifests must contain both `apiVersion` and `kind` fields to be recognized as Kubernetes resources

The `KubernetesComponentDetector` is a **DefaultOff** detector and must be explicitly enabled via the `--DetectorArgs` parameter.

## Detection strategy

The Kubernetes detector parses Kubernetes manifest YAML files to extract Docker image references from container specifications.

### Supported Resource Kinds

The detector recognizes the following Kubernetes resource kinds:

- `Pod`
- `Deployment`
- `StatefulSet`
- `DaemonSet`
- `ReplicaSet`
- `Job`
- `CronJob`
- `ReplicationController`

Files with an unrecognized `kind` or missing `apiVersion`/`kind` fields are skipped.

### Container Image Detection

The detector extracts image references from all container types within pod specifications:

- **containers**: Main application containers
- **initContainers**: Initialization containers that run before app containers
- **ephemeralContainers**: Ephemeral debugging containers

### Pod Spec Locations

The detector handles different pod spec locations depending on the resource kind:

- **Pod**: `spec.containers`
- **Deployment, StatefulSet, DaemonSet, ReplicaSet, ReplicationController**: `spec.template.spec.containers`
- **Job**: `spec.template.spec.containers`
- **CronJob**: `spec.jobTemplate.spec.template.spec.containers`

### Variable Resolution

Images containing unresolved variables (e.g., `${TAG}`) are skipped to avoid reporting incomplete or incorrect references. The detector checks for `$`, `{`, or `}` characters in image references.

## Known limitations

- **DefaultOff Status**: This detector must be explicitly enabled using `--DetectorArgs Kubernetes=EnableIfDefaultOff`
- **Broad File Matching**: The `*.yaml` and `*.yml` search patterns match all YAML files, so the detector relies on content-based filtering (`apiVersion` and `kind` fields) to identify Kubernetes manifests
- **Variable Resolution**: Image references containing unresolved template variables are not reported
- **Limited Resource Kinds**: Only the eight resource kinds listed above are supported. Custom resources (CRDs) or other workload types are not detected
- **No Dependency Graph**: All detected images are registered as independent components without parent-child relationships

# Linux Detection

## Requirements

Linux detection depends on the following:

- [Docker](https://www.docker.com/)

## Detection strategy

Linux package detection is performed by running [Syft](https://github.com/anchore/syft) and parsing the output.
The output contains the package name, version, and the layer of the container in which it was found.

### Supported Input Types

The Linux detector runs on container images passed under the `--DockerImagesToScan` flag.

Supported image reference formats are:

#### Name and Tag/Digest

Images in the local Docker daemon or a remote registry can be referenced by name and tag or digest. For example, `ubuntu:16.04`. Remote images will be pulled if they are not present locally.

#### Digest Only

Images already present in the local Docker daemon can be referenced by just a digest. For example, `sha256:56bab49eef2ef07505f6a1b0d5bd3a601dfc3c76ad4460f24c91d6fa298369ab`.

#### OCI Images

Images present on the filesystem as either an [OCI layout directory](https://specs.opencontainers.org/image-spec/image-layout/) or an OCI image archive (tarball) can be referenced by file path.

- For OCI image layout directories, use the prefix `oci-dir:` followed by the path to the directory, e.g. `oci-dir:/path/to/image`
- For OCI image archives (tarballs), use the prefix `oci-archive:` followed by the path to the archive file, e.g. `oci-archive:/path/to/image.tar`

#### Docker Archives

Images saved to disk via `docker save` can be referenced using the `docker-archive:` prefix followed by the path to the tarball, e.g. `docker-archive:/path/to/image.tar`.

### Platform Selection

For multi-platform local images, append `?platform=<platform>` to each image reference. Platform selection is supported for OCI layouts, OCI archives, and Docker archives. The value is passed directly to Syft, which accepts forms such as `linux/arm64`, `linux/arm64/v8`, `arm64`, and `linux`.

For example, different platforms can be selected for multiple images in one scan:

```sh
--DockerImagesToScan "oci-archive:/images/api.tar?platform=linux/amd64,oci-archive:/images/worker.tar?platform=linux/arm64"
```

The same local image can also be scanned once per platform:

```sh
--DockerImagesToScan "oci-dir:/images/application?platform=linux/amd64,oci-dir:/images/application?platform=linux/arm64"
```

When the platform suffix is omitted, Syft uses the current system platform, or if the image only has a single platform, that one is chosen. Platform selection is not supported for image names, tags, or digests resolved through the Docker daemon because Docker selects their platform before Syft scans them.

### Scanner Scope

By default, this detector invokes Syft with the `all-layers` scanning scope (i.e. the Syft argument `--scope all-layers`).

Syft has another scope, `squashed`, which can be used to scan only files accessible from the final layer of an image.

The detector argument `Linux.ImageScanScope` can be used to configure this option as `squashed` or `all-layers` when invoking Component Detection.

For example:

```sh
--DetectorArgs Linux.ImageScanScope=squashed
```

## Known limitations

- Windows container scanning is not supported
- Platform selection is not supported for image names, tags, or digests resolved through the Docker daemon

## Excluding Base Image Components

When scanning container images, many detected components may originate from the base image rather than from layers added by the user's Dockerfile. The `--ExcludeBaseImageComponents` flag filters out components that exist exclusively in base image layers, so only components introduced by the user's own layers are reported.

```sh
--ExcludeBaseImageComponents
```

A component is excluded only if **all** of its associated container layers are marked as base image layers. If a component appears in at least one non-base-image layer, it is retained.

This flag has no effect on non-container scans (i.e., directory-based detection).

# Linux Detection

## Requirements

Linux detection depends on the following:

- [Docker](https://www.docker.com/)

## Detection strategy

Linux package detection is performed by running [Syft](https://github.com/anchore/syft) and parsing the output.
The output contains the package name, version, and the layer of the container in which it was found.

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

# Linux Detection

## Requirements

Linux detection depends on the following:

- [Docker](https://www.docker.com/)

## Detection strategy

Linux package detection is performed by running [Syft](https://github.com/anchore/syft) and parsing the output.
The output contains the package name, version, and the layer of the container in which it was found.

## Known limitations

- Windows container scanning is not supported

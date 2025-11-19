# SPDX Detection

## Requirements

SPDX detection depends on the following to successfully run:

- One or more `*.spdx.json` files in the scan directory

## Detection strategy

The SPDX detector (`Spdx22ComponentDetector`) discovers SPDX SBOM (Software Bill of Materials) files in JSON format and creates components representing the SPDX documents themselves.

The detector:
- Searches for files matching the pattern `*.spdx.json`
- Validates that the SPDX version is `SPDX-2.2` (currently the only supported version)
- Computes a SHA-1 hash of the SPDX file for identification
- Extracts metadata including:
  - Document namespace
  - Document name
  - SPDX version
  - Root element ID from `documentDescribes` (defaults to `SPDXRef-Document` if not specified)
- Creates an `SpdxComponent` to represent the SPDX document

The detector does not parse or register individual packages listed within the SPDX document; it only registers the SPDX document itself as a component.

## Known limitations

- Only SPDX version 2.2 is currently supported
- Only JSON format is supported (`.spdx.json` files)
- The detector is **DefaultOff** and must be explicitly enabled via detector arguments
- If an SPDX document contains multiple elements in `documentDescribes`, only the first element is selected as the root element
- The detector does not create a dependency graph from the packages listed within the SPDX document
- Invalid JSON files or files that cannot be parsed are skipped with a warning

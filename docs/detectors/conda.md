# Conda Detection

## Requirements

The lock file produced by `conda-lock` will be used. Accordingly the only requirement is that this file is present.

## Default Detection strategy

The detector will parse `conda-lock.json` and `*.conda-lock.json` files. The full dependency graph is generated based on the information in these files.

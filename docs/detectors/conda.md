# Conda Detection

## Requirements

The lock file produced by `conda-lock` will be used. Accordingly the only requirement is that this file is present.

## Default Detection strategy

The detector will parse `conda-lock.json` and `*.conda-lock.json` files. The lock file contains a reference to the `environment.yml` file that was used to generate the lock file. The detector will try to parse the referenced environment.yml file as well.

The `environment.yml` file is parsed to determine which packages are directly referenced. Afterwards, starting from these packages the dependency tree will be build up. If the `environment.yml` file cannot be parsed (e.g., it is not present for some reason), all files in the conda-lock file will be considered directly referenced. This is done, to avoid that no alerts will be fired at all, if the `environment.yml` cannot be parsed. 

## Possible future improvements

If the `environment.yml` file cannot be parsed, the dependency graph can be build bottom up. That is, start with packages that don’t have any other dependencies and traverse the tree bottom up. Finally, all packages that are not references by any other packages are considered directly referenced. 

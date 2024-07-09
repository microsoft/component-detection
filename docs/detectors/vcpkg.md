# vcpkg Detection

## Requirements

vcpkg detection triggers off of `vcpkg.spdx.json` files found under the scan directory. You must use a version of vcpkg in your build that generates SBOM files (newer than 2022-05-05).

## Detection strategy

The vcpkg detector searches for `vcpkg.spdx.json` files produced by vcpkg during the install process. These files are typically found under the installed packages directory in a path like `installed/<triplet>/share/<port>/vcpkg.spdx.json`. Each vcpkg port installes a separate `vcpkg.spdx.json` file[1].

Because this detection strategy looks for the concrete files in the installed tree, it will accurately detect the precise packages used
during this build and exclude packages optionally used on other platforms.

## Known limitations

The vcpkg detector does not distinguish between direct dependencies and transitive dependencies. It also does not distinguish
"development-only" dependencies that are not intended to impact the final shipping product.

[1]: https://learn.microsoft.com/vcpkg/reference/software-bill-of-materials

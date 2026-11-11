# Composer

The Composer detector finds packages used by PHP projects, including development
dependencies and the packages they depend on. PHP and Composer do not need to be
installed to scan the project's package files.

## How detection works

The detector looks for `composer.lock` and `composer.json` in your project:

1. **If a lockfile exists**, it uses the package versions recorded there.
   The `composer.json` in the same folder helps identify which packages your
   project uses directly.
2. **If there is no lockfile**, it looks for installed package information in
   `vendor\composer\installed.json`.
3. **If only `composer.json` is available**, it reports dependencies pinned to
   exact versions, such as `1.2.3`. Ranges such as `^1.2` are skipped with a warning,
   because they do not tell us which version was actually installed.

For a custom vendor directory, the detector first checks the
[`COMPOSER_VENDOR_DIR` environment variable](https://getcomposer.org/doc/03-cli.md#composer_vendor_dir),
then `config.vendor-dir` in `composer.json`. Otherwise, it uses `vendor`.

For more accurate results, run `composer install` before scanning so the lockfile
and installed package information are available.

Results include dependency relationships and development flags when that
information is available. If your repository contains several PHP projects, each
is scanned separately. Vendor packages already covered by a parent project are
not scanned again.

## PHP environment

When PHP and Composer are available on `PATH`, the detector can also report the
local PHP version, Composer, extensions, and related libraries. It checks each
project separately and does not run this check inside vendor folders.

These entries use the `PhpSdk` component type and are marked as development
dependencies. They describe the machine running the scan, not necessarily the
environment where your application will run. Excluding development dependencies
also excludes these entries. Simulated versions from `config.platform` are not
reported as installed software.

Missing tools or a failed environment check do not stop package detection.
The scan does not install or update packages.

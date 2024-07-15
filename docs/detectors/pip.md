# Pip Detection

## Requirements

Pip detection depends on the following to successfully run:

- Python 2 or Python 3
- Internet connection
- One or more `setup.py` or `requirements.txt` files

## Detection strategy

### Installation Report (PipReportDetector)
The `--report` option of the `pip install` command produces a detailed JSON report of what it did install (or what it would have installed). 
See https://pip.pypa.io/en/stable/reference/installation-report/#specification for more details.

Serialization specifications:
- https://packaging.python.org/en/latest/specifications/core-metadata/
- https://peps.python.org/pep-0508/
- https://peps.python.org/pep-0301/

The detector can also pick up installation reports that have already been generated in the same directory as the `setup.py` or `requirements.txt` files, 
as long as the report adheres to the following naming scheme: `component-detection-pip-report.json` or `*.component-detection-pip-report.json`

### Legacy Detection (PipDetector, SimplePipDetector)

Pip detection is performed by running the following code snippet on every *setup.py*:

```python
    import distutils.core;
    setup = distutils.core.run_setup({setup.py});
    print(setup.install_requires);
```

The code above allows Pip detection to detect any runtime dependencies.

`requirements.txt` files are parsed; a Git component is created for every `git+` url.

For every top level component, Pip detection makes http calls to Pip in order to determine latest version available, as well as to resolve the dependency tree by parsing the `METADATA` file on a given release's `bdist_wheel` or `bdist_egg`.

Full dependency graph generation is supported.

## Known limitations

Dev dependency tagging is not supported.

Pip detection will not run if `python` is unavailable.

If no `bdist_wheel` or `bdist_egg` are available for a given component, dependencies will not be fetched.

If no internet connection or a component cannot be found in PyPi, said component and its dependencies will be skipped.

## Environment Variables

The environment variable `PyPiMaxCacheEntries` is used to control the size of the in-memory LRU cache that caches responses from PyPi.
The default value is 4096.

The enviroment variable `PIP_INDEX_URL` is used to determine what package feed should be used for `pip install --report` detection.
The default value will use the PyPi index unless pip defaults have been configured globally.

The environment variable `PipReportOverrideBehavior` is used to override pip report with one of the following detection strategies.
- `Skip`: Will not run pip detection
- `SourceCodeScan`: Scan `setup.py` and `requirements.txt` files, and record components explicitly from the package files without hitting a remote feed. Does not compile a dependency graph.

The environment variable `PipReportSkipFallbackOnFailure` is used to skip the default fallback behavior if pip report fails. Default behavior scans `setup.py` and `requirements.txt` files, and record components explicitly from the package files without hitting a remote feed. Does not compile a dependency graph.

The environment variable `PipReportFileLevelTimeoutSeconds` is used to control the timeout limit for generating the PipReport for individual files. This defaults to the overall timeout.

The environment variable `PipReportDisableFastDeps` is used to disable the fast deps feature in PipReport.

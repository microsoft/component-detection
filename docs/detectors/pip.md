# Pip Detection

## Requirements

Pip detection depends on the following to successfully run:

- Python 2 or Python 3
- Internet connection
- One or more `setup.py` or `requirements.txt` files

## Detection strategy

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

If no internet connection or a component cannot be found in Pypi, said component and its dependencies will be skipped.

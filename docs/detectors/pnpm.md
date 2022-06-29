# Pnpm detection

## Known limitations

The Pnpm detector doesn't support the resolution of local dependencies
like:

- Link dependencies
```
dependencies:
      '@learningclient/common': link:../common
```

- File dependencies
```
dependencies:
    file:./projects/gmc-bootstrapper.tgz
```
These kind of components are ignored by the Pnpm detector.

In the case of `link` dependencies that refer to a folder with a `package.json` file
the component is then going to be detected by the `NpmComponentDetector`. This is going to happen
only if the folder is inside the path that is been use for scanning.

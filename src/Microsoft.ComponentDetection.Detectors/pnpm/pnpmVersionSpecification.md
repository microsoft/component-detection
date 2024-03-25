# Pnpm Version format specification

[Offical documentation for the lock file formats used by pnpm](https://github.com/pnpm/spec/tree/master/lockfile).

Below is a summary of some conversations with the Pnpm community about how the version is formatted for pnpm packages:

A dependency defined in package.json as:
```
jquery: 1.0.0
```
is going to be represented in the version 5 lock files of pnpm as:
```
/jquery/1.0.0
```
and in version 6 lock files of pnpm as:
```
/jquery@1.0.0
```


This nomenclature is known for the pnpm community as the package path, and is normally found as the package definition in the section Packages of the lock files (pnpm-lock.yml and shrinkwrap.yml).
Normally most of the packages has this structure but there others situations like [peer dependencies](https://pnpm.js.org/en/how-peers-are-resolved), where the path format can change to represent the peer dependency combination, for example:

First the regular case, suppose that we have in the package.json a dependency defined as

```
{
    name: foo
    version: 1.0.0
    dependencies: {
        abc: 1.0.0
    }
}
```
using the command pnpm install this is going to create a folder structure like
```
/foo/1.0.0/
|- foo (hardlink)
|- abc (symlink)
```
and the entry in the lock file is going to looks like
```
/foo/1.0.0
```
Now if the package foo define a peer dependency as:

```
{
    name: foo
    version: 1.0.0
    dependencies: {
        abc: 1.0.0
    },
    peerDependencies: {
        bar: ^1.0.0
    }
}
```
And foo has diferent parents that dependents on this peer dependency but using a different version that still satisfy the semantic version defined by foo:

```
{
    name: parent1
    version: 1.0.0
    dependencies: {
        foo: 1.0.0
        bar: 1.0.0
    }
}


{
    name: parent2
    version: 1.0.0
    dependencies: {
        foo: 1.0.0
        bar: 1.0.1
    }
}
```

then Pnpm is going to create a new folder for each combination _foo_  _bar 1.0.0_ and _foo_  _bar 1.0.1_

The folder structure would looks like

```
/foo/1.0.0
|-bar@1.0.0
  |- foo(hardlink)
  |- bar v1.0.0(symlink)
  |- abc (symlink)
|-bar@1.0.1
  |- foo(hardlink)
  |- bar v1.0.1(symlink)
  |- abc(symlink)
```
This is going to create 2 different entries in the lock file, these entries can have a different format depending on the version of pnpm, they can look like:
```
/foo/1.0.0/bar@1.0.0
/foo/1.0.0/bar@1.0.1
```
or
```
/foo/1.0.0_bar@1.0.0
/foo/1.0.0_bar@1.0.1
```


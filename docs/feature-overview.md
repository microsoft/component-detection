# Feature Overview

| Ecosystem | Detection Mechanism | Requirements | Development Dependencies labeling | Graph Creation |
| - | - | - | - | - |
| CocoaPods | <ul><li>podfile.lock</li></ul> | - | ❌ | - |
| Conda (Python) (Beta) | <ul><li>environment.yml</li><li>environment.yaml</li></ul> | <ul><li>Conda v4.10.2+</li></ul> | ❌ | ❌ |
| Linux (Debian, Alpine, Rhel, Centos, Fedora, Ubuntu)| <ul><li>(via [syft](https://github.com/anchore/syft))</li></ul> | - | - | - | - |
| Gradle | <ul><li>*.lockfile</li></ul> | <ul><li>Gradle 7 or prior using [Single File lock](https://docs.gradle.org/6.8.1/userguide/dependency_locking.html#single_lock_file_per_project)</li></ul> | ❌ | ❌ |
| Go | <ul><li>*go list -m -json all*</li><li>*go mod graph* (edge information only)</li></ul>Fallback</br><ul><li>go.mod</li><li>go.sum</li></ul> | <ul><li>Go 1.11+ (will fallback if not present)</li></ul> | ❌ | ✔ (root idenditication only for fallback) |
| Maven | <ul><li>pom.xml</li><li>*mvn dependency:tree -f {pom.xml}*</li></ul> | <ul><li>Maven</li><li>Maven Dependency Plugin (auto-installed with Maven)</li></ul> | ✔ (test dependency scope) | ✔ |
| NPM | <ul><li>package.json</li><li>package-lock.json</li><li>npm-shrinkwrap.json</li><li>lerna.json</li></ul> | - | ✔ (dev-dependencies in package.json, dev flag in package-lock.json) | ✔ |
| Yarn (v1, v2) | <ul><li>package.json</li><li>yarn.lock</li></ul> | - | ✔ (dev-dependencies in package.json) | ✔ |
| Pnpm | <ul><li>shrinkwrap.yaml</li><li>pnpm-lock.yaml</li></ul> | - | ✔ (packages/{package}/dev flag) | ✔ |
| NuGet | <ul><li>project.assets.json</li><li>*.nupkg</li><li>*.nuspec</li><li>nuget.config</li></ul> | - | - | ✔ (required project.assets.json) |
| Pip (Python) | <ul><li>setup.py</li><li>requirements.txt</li><li>*setup=distutils.core.run_setup({setup.py}); setup.install_requires*</li><li>dist package METADATA file</li></ul> | <ul><li>Python 2 or Python 3</li><li>Internet connection</li></ul> | ❌ | ✔ |
| Poetry (Python) | <ul><li>poetry.lock</li><ul> | - | ✔ | ❌ |
| Ruby | <ul><li>gemfile.lock</li></ul> | - | ❌ | ✔ |
| Cargo | <ul><li>Cargo.lock (v1, v2, v3)</li></ul> | - | ❌ | ✔ | 


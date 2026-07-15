#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.CondaLock;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class CondaLockComponentDetectorTests
{
    private readonly DetectorTestUtilityBuilder<CondaLockComponentDetector> detectorTestUtility = new();

    [TestMethod]
    public async Task CondaComponentDetector_TestCondaLockFileAsync()
    {
        // A reduced version of the full conda lock file is used for this test
        var condaLockContent =
@"version: 1
metadata:
  content_hash:
    osx-64: 1448e343b4d8a617cda801da72ad04b5aa5d3bf7d8ad17ad1d86ab3788216bd2
    linux-64: 0fc90bb13c2014c59b9d5dfb6d82f86db309d511aae307c0868310f170841c96
    win-64: c88dea8cfbca2f9ce0cae14272db0bbed3788d286f04153a898f49743a7311f7
  channels:
  - url: defaults
    used_env_vars: []
  platforms:
  - osx-64
  - linux-64
  - win-64
  sources:
  - environment.yml
package:
- name: requests
  version: 2.31.0
  manager: pip
  platform: linux-64
  dependencies:
    certifi: '>=2017.4.17'
  url: https://files.pythonhosted.org/packages/70/8e/0e2d847013cb52cd35b38c009bb167a1a26b2ce6cd6965bf26b47bc0bf44/requests-2.31.0-py3-none-any.whl
  hash:
    sha256: 58cd2187c01e70e6e26505bca751777aa9f2ee0b7f4300988b709f44e013003f
  category: main
  optional: false
- name: certifi
  version: 2023.5.7
  manager: pip
  platform: linux-64
  dependencies: {}
  url: https://files.pythonhosted.org/packages/9d/19/59961b522e6757f0c9097e4493fa906031b95b3ebe9360b2c3083561a6b4/certifi-2023.5.7-py3-none-any.whl
  hash:
    sha256: c6c2e98f5c7869efca1f8916fed228dd91539f9f1b444c314c06eef02980c716
  category: main
  optional: false
- name: conda-lock
  version: 2.1.0
  manager: conda
  platform: linux-64
  dependencies:
    urllib3: '>=1.26.5,<2.0'
  url: https://conda.anaconda.org/conda-forge/noarch/conda-lock-2.1.0-pyhd8ed1ab_0.conda
  hash:
    md5: 1e07afcf3d3e371fc3a3681fe9b78e90
    sha256: 05319e84cbd36f6a05563954d2dbff041de6ece406a59650784918026080c98c
  category: main
  optional: false
- name: urllib3
  version: 1.26.16
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://repo.anaconda.com/pkgs/main/linux-64/urllib3-1.26.16-py311h06a4308_0.conda
  hash:
    md5: 4b62a74f7e797800039971833968e23f
    sha256: b9e919a9bcb4cb291fe60952895bf0c3ce9dbcbeaa3d5706131f862756fabc40
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // packages from the conda section
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "conda-lock", "2.1.0");
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "urllib3", "1.26.16");

        // packages from the pip section
        this.AssertPipComponentNameAndVersion(detectedComponents, "certifi", "2023.5.7");
        this.AssertPipComponentNameAndVersion(detectedComponents, "requests", "2.31.0");

        detectedComponents.Should().HaveCount(4);
    }

    [TestMethod]
    public async Task CondaComponentDetector_TestCyclicalDependenciesAsync()
    {
        // conda environments can contain cyclical dependencies (e.g. pip <-> setuptools).
        // This lock file models a cycle: pkg-a -> pkg-b -> pkg-a.
        // The detector must handle it without infinite recursion and still register both packages.
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  sources:
  - environment.yml
package:
- name: pkg-a
  version: 1.0.0
  manager: conda
  platform: linux-64
  dependencies:
    pkg-b: '>=1.0.0'
  url: https://conda.anaconda.org/conda-forge/noarch/pkg-a-1.0.0-0.conda
  hash:
    sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  category: main
  optional: false
- name: pkg-b
  version: 2.0.0
  manager: conda
  platform: linux-64
  dependencies:
    pkg-a: '>=1.0.0'
  url: https://conda.anaconda.org/conda-forge/noarch/pkg-b-2.0.0-0.conda
  hash:
    sha256: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
  category: main
  optional: false
";

        var detectorTask = this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        // If the detector fails to guard against cyclical dependencies it will spin
        // (or recurse) forever. Race the detector against a timeout so the test fails
        // fast with a clear message instead of hanging the whole test run.
        var completed = await Task.WhenAny(detectorTask, Task.Delay(3000));
        completed.Should().BeSameAs(detectorTask, "the detector should handle cyclical dependencies without spinning");

        var (scanResult, componentRecorder) = await detectorTask;

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "pkg-a", "1.0.0");
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "pkg-b", "2.0.0");

        detectedComponents.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task CondaComponentDetector_ManagerDeterminesComponentTypeAsync()
    {
        // A conda-managed package that depends on python must still be a CondaComponent
        // (it comes from a conda channel, not PyPI). Only pip-managed packages become PipComponents.
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  sources:
  - environment.yml
package:
- name: numpy
  version: 1.24.0
  manager: conda
  platform: linux-64
  dependencies:
    python: '>=3.8'
  url: https://conda.anaconda.org/conda-forge/linux-64/numpy-1.24.0-py311.conda
  hash:
    sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  category: main
  optional: false
- name: python
  version: 3.11.0
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/linux-64/python-3.11.0.conda
  hash:
    sha256: cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc
  category: main
  optional: false
- name: boto3
  version: 1.28.0
  manager: pip
  platform: linux-64
  dependencies:
    python: '>=3.7'
  url: https://files.pythonhosted.org/packages/boto3-1.28.0-py3-none-any.whl
  hash:
    sha256: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // conda-managed packages remain conda components even when they depend on python.
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "numpy", "1.24.0");
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "python", "3.11.0");

        // Only the pip-managed package is a pip component.
        this.AssertPipComponentNameAndVersion(detectedComponents, "boto3", "1.28.0");
        detectedComponents.Count(c => c.Component is PipComponent).Should().Be(1);

        detectedComponents.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task CondaComponentDetector_HandlesMissingDependenciesFieldAsync()
    {
        // A package may omit the "dependencies" field entirely (deserializes to null).
        // The detector must not throw and must still register the package.
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  sources:
  - environment.yml
package:
- name: solo
  version: 1.0.0
  manager: conda
  platform: linux-64
  url: https://conda.anaconda.org/conda-forge/linux-64/solo-1.0.0-0.conda
  hash:
    sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "solo", "1.0.0");
        detectedComponents.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task CondaComponentDetector_DeduplicatesPackagesAcrossPlatformsAsync()
    {
        // The same package/version appears once per platform with platform-specific urls.
        // Because the url is intentionally dropped, these collapse into a single component.
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  - win-64
  sources:
  - environment.yml
package:
- name: zlib
  version: 1.2.13
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/linux-64/zlib-1.2.13-0.conda
  hash:
    sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  category: main
  optional: false
- name: zlib
  version: 1.2.13
  manager: conda
  platform: win-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/win-64/zlib-1.2.13-0.conda
  hash:
    sha256: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "zlib", "1.2.13");
        detectedComponents.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task CondaComponentDetector_RecordsDependencyGraphRelationshipsAsync()
    {
        // parent-pkg -> child-pkg. The root is explicitly referenced; the transitive
        // dependency is not, and the parent->child edge is recorded.
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  sources:
  - environment.yml
package:
- name: parent-pkg
  version: 1.0.0
  manager: conda
  platform: linux-64
  dependencies:
    child-pkg: '>=1.0.0'
  url: https://conda.anaconda.org/conda-forge/linux-64/parent-pkg-1.0.0-0.conda
  hash:
    sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  category: main
  optional: false
- name: child-pkg
  version: 2.0.0
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/linux-64/child-pkg-2.0.0-0.conda
  hash:
    sha256: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().HaveCount(2);

        var parentId = detectedComponents.Single(c => c.Component is CondaComponent { Name: "parent-pkg" }).Component.Id;
        var childId = detectedComponents.Single(c => c.Component is CondaComponent { Name: "child-pkg" }).Component.Id;

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.Single();

        graph.IsComponentExplicitlyReferenced(parentId).Should().BeTrue();
        graph.IsComponentExplicitlyReferenced(childId).Should().BeFalse();

        graph.GetDependenciesForComponent(parentId).Should().Contain(childId);
        graph.GetDependenciesForComponent(childId).Should().BeEmpty();
    }

    [TestMethod]
    public async Task CondaComponentDetector_HandlesFileWithNoPackagesAsync()
    {
        // A lock file with no package section should produce no components and still succeed.
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  sources:
  - environment.yml
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CondaComponentDetector_DetectsNamedCondaLockFileAsync()
    {
        // The detector also matches "*.conda-lock.yml" (e.g. environment-specific lock files).
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  sources:
  - environment.yml
package:
- name: openssl
  version: 3.1.0
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/linux-64/openssl-3.1.0-0.conda
  hash:
    sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("environment.conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.AssertCondaLockComponentNameAndVersion(detectedComponents, "openssl", "3.1.0");
        detectedComponents.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task CondaComponentDetector_HandlesMalformedYamlGracefullyAsync()
    {
        // A malformed lock file should be caught, logged, and not fail the overall scan.
        var condaLockContent =
@"version: 1
package:
- name: [this is not valid
  version: : : :
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().BeEmpty();
    }

    private void AssertCondaLockComponentNameAndVersion(IEnumerable<DetectedComponent> detectedComponents, string name, string version)
    {
        detectedComponents.SingleOrDefault(c =>
                c.Component is CondaComponent component &&
                component.Name.Equals(name) &&
                component.Version.Equals(version)).Should().NotBeNull(
            $"Component with name {name} and version {version} was not found");
    }

    private void AssertPipComponentNameAndVersion(IEnumerable<DetectedComponent> detectedComponents, string name, string version)
    {
        detectedComponents.SingleOrDefault(c =>
                c.Component is PipComponent component &&
                component.Name.Equals(name) &&
                component.Version.Equals(version)).Should().NotBeNull(
            $"Component with name {name} and version {version} was not found");
    }
}

namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class CondaComponentDetectorTests : BaseDetectorTest<CondaComponentDetector>
{
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

        var condaEnvironmentContent =
@"name: cgtest
dependencies:
  - conda-forge::conda-lock=2.1.0
  - pip:
    - requests
channels:
  - defaults
";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .WithFile("environment.yml", condaEnvironmentContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        // packages from the conda section
        this.AssertCondaComponentNameAndVersion(detectedComponents, "conda-lock", "2.1.0");
        this.AssertCondaComponentNameAndVersion(detectedComponents, "urllib3", "1.26.16");

        // packages from the pip section
        this.AssertCondaComponentNameAndVersion(detectedComponents, "certifi", "2023.5.7");
        this.AssertCondaComponentNameAndVersion(detectedComponents, "requests", "2.31.0");

        Assert.AreEqual(4, detectedComponents.Count());
    }

    private void AssertCondaComponentNameAndVersion(IEnumerable<DetectedComponent> detectedComponents, string name, string version)
    {
        Assert.IsNotNull(
            detectedComponents.SingleOrDefault(c =>
                c.Component is CondaComponent component &&
                component.Name.Equals(name) &&
                component.Version.Equals(version)),
            $"Component with name {name} and version {version} was not found");
    }
}
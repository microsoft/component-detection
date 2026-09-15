#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
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
  build: pyhd8ed1ab_0
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
        var condaLockComponent = detectedComponents
            .Select(component => component.Component)
            .OfType<CondaComponent>()
            .Single(component => component.Name == "conda-lock");
        condaLockComponent.Version.Should().Be("2.1.0");
        condaLockComponent.Build.Should().Be("pyhd8ed1ab_0");
        condaLockComponent.Channel.Should().Be("https://conda.anaconda.org/conda-forge");
        condaLockComponent.Subdir.Should().Be("noarch");
        condaLockComponent.Url.Should().Be("https://conda.anaconda.org/conda-forge/noarch/conda-lock-2.1.0-pyhd8ed1ab_0.conda");
        condaLockComponent.DownloadUrl.Should().Be(
            new Uri("https://conda.anaconda.org/conda-forge/noarch/conda-lock-2.1.0-pyhd8ed1ab_0.conda"));
        condaLockComponent.MD5.Should().Be("1e07afcf3d3e371fc3a3681fe9b78e90");
        condaLockComponent.SHA256.Should().Be("05319e84cbd36f6a05563954d2dbff041de6ece406a59650784918026080c98c");

        var urllibComponent = detectedComponents
            .Select(component => component.Component)
            .OfType<CondaComponent>()
            .Single(component => component.Name == "urllib3");
        urllibComponent.Version.Should().Be("1.26.16");
        urllibComponent.Build.Should().Be("py311h06a4308_0");
        urllibComponent.Channel.Should().Be("https://repo.anaconda.com/pkgs/main");
        urllibComponent.Subdir.Should().Be("linux-64");
        urllibComponent.Url.Should().Be("https://repo.anaconda.com/pkgs/main/linux-64/urllib3-1.26.16-py311h06a4308_0.conda");
        urllibComponent.MD5.Should().Be("4b62a74f7e797800039971833968e23f");
        urllibComponent.SHA256.Should().Be("b9e919a9bcb4cb291fe60952895bf0c3ce9dbcbeaa3d5706131f862756fabc40");

        // packages from the pip section
        this.AssertPipComponentNameAndVersion(detectedComponents, "certifi", "2023.5.7");
        this.AssertPipComponentNameAndVersion(detectedComponents, "requests", "2.31.0");
        var requestsComponent = detectedComponents
            .Select(component => component.Component)
            .OfType<PipComponent>()
            .Single(component => component.Name == "requests");
        requestsComponent.DownloadUrl.Should().Be(
            new Uri("https://files.pythonhosted.org/packages/70/8e/0e2d847013cb52cd35b38c009bb167a1a26b2ce6cd6965bf26b47bc0bf44/requests-2.31.0-py3-none-any.whl"));

        detectedComponents.Should().HaveCount(4);
    }

    [TestMethod]
    public async Task CondaComponentDetector_NoarchPackagesAreDeduplicatedAcrossPlatformsAsync()
    {
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  - win-64
package:
- name: colorama
  version: 0.4.6
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/noarch/colorama-0.4.6-pyhd8ed1ab_1.conda
  hash:
    md5: 7c68c537b61ad0c41aa7f1c2a8a6bd6f
    sha256: 45a2c7b5c2146b9f8e3a879284cf1c7c40264b4e3fd40fdbdbcf34a10f522f4d
  category: main
  optional: false
- name: colorama
  version: 0.4.6
  manager: conda
  platform: win-64
  dependencies: {}
  url: https://conda.anaconda.org/conda-forge/noarch/colorama-0.4.6-pyhd8ed1ab_1.conda
  hash:
    md5: 7c68c537b61ad0c41aa7f1c2a8a6bd6f
    sha256: 45a2c7b5c2146b9f8e3a879284cf1c7c40264b4e3fd40fdbdbcf34a10f522f4d
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = componentRecorder.GetDetectedComponents().Single().Component.Should().BeOfType<CondaComponent>().Subject;
        component.Build.Should().Be("pyhd8ed1ab_1");
        component.Subdir.Should().Be("noarch");
        component.SHA256.Should().Be("45a2c7b5c2146b9f8e3a879284cf1c7c40264b4e3fd40fdbdbcf34a10f522f4d");
    }

    [TestMethod]
    public async Task CondaComponentDetector_PlatformPackagesUseSeparateGraphNodesAsync()
    {
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
  - osx-64
  - win-64
package:
- name: sample
  version: 1.0.0
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://conda.example/channel/linux-64/sample-1.0.0-linux_0.conda
  hash:
    md5: linux
- name: sample
  version: 1.0.0
  manager: conda
  platform: osx-64
  dependencies: {}
  url: https://conda.example/channel/osx-64/sample-1.0.0-osx_0.conda
  hash:
    md5: osx
- name: sample
  version: 1.0.0
  manager: conda
  platform: win-64
  dependencies: {}
  url: https://conda.example/channel/win-64/sample-1.0.0-win_0.conda
  hash:
    md5: windows
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var components = componentRecorder.GetDetectedComponents()
            .Select(component => component.Component)
            .OfType<CondaComponent>()
            .ToList();
        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.Single();

        components.Should().HaveCount(3);
        components.Select(component => component.BaseId).Distinct().Should().ContainSingle()
            .Which.Should().Be("sample 1.0.0 - Conda");
        components.Select(component => component.Id).Should().OnlyHaveUniqueItems();
        components.Should().OnlyContain(component => graph.Contains(component.Id));
    }

    [TestMethod]
    public async Task CondaComponentDetector_CondaPackageWithoutMd5IsRecordedAsync()
    {
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
package:
- name: sample
  version: 1.0.0
  build: custom_0
  manager: conda
  platform: linux-64
  dependencies: {}
  url: https://example.test/channel/linux-64/sample-1.0.0-different_1.conda
  hash:
    sha256: d2d2d2d2
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = componentRecorder.GetDetectedComponents().Single().Component.Should().BeOfType<CondaComponent>().Subject;
        component.Build.Should().Be("custom_0");
        component.MD5.Should().BeNull();
        component.SHA256.Should().Be("d2d2d2d2");
    }

    [TestMethod]
    [DataRow(".conda")]
    [DataRow(".tar.bz2")]
    public async Task CondaComponentDetector_ExtractsBuildFromPackageFileNameAsync(string packageExtension)
    {
        var packageUrl = $"https://conda.example/channel/linux-64/sample-1.0.0-build_0{packageExtension}";
        var condaLockContent =
$@"version: 1
metadata:
  platforms:
  - linux-64
package:
- name: sample
  version: 1.0.0
  manager: conda
  platform: linux-64
  dependencies: {{}}
  url: {packageUrl}
  hash:
    md5: d2d2d2d2
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var component = componentRecorder.GetDetectedComponents().Single().Component.Should().BeOfType<CondaComponent>().Subject;
        component.Build.Should().Be("build_0");
        component.DownloadUrl.Should().Be(new Uri(packageUrl));
        component.Id.Should().Contain("Build:build_0");
    }

    [TestMethod]
    public async Task CondaComponentDetector_CircularDependenciesDoNotOverflowAsync()
    {
        var condaLockContent =
@"version: 1
metadata:
  platforms:
  - linux-64
package:
- name: python
  version: 3.12.13
  manager: conda
  platform: linux-64
  dependencies:
    pip: ''
  category: main
  optional: false
- name: pip
  version: 26.2.1
  manager: conda
  platform: linux-64
  dependencies:
    python: ''
  category: main
  optional: false
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("conda-lock.yml", condaLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();
        var pythonId = detectedComponents.Single(component => component.Component is CondaComponent { Name: "python" }).Component.Id;
        var pipId = detectedComponents.Single(component => component.Component is PipComponent { Name: "pip" }).Component.Id;

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        detectedComponents.Should().HaveCount(2);
        dependencyGraph.GetDependenciesForComponent(pythonId).Should().Contain(pipId);
        dependencyGraph.GetDependenciesForComponent(pipId).Should().Contain(pythonId);
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

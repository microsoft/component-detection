#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.CocoaPods;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PodDetectorTest : BaseDetectorTest<PodComponentDetector>
{
    [TestMethod]
    public async Task TestPodDetector_EmptyPodfileLockAsync()
    {
        var podfileLockContent = @"PODFILE CHECKSUM: b3f970aecf9d240064c3b1737d975c9cb179c851

COCOAPODS: 1.4.0.beta.1";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizePodComponentsAsync()
    {
        var podfileLockContent = @"PODS:
  - AzureCore (0.5.0):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - AzureData (0.5.0):
    - AzureCore (= 0.5.0)
  - AzureMobile (0.5.0):
    - AzureData (= 0.5.0)
  - KeychainAccess (3.2.1)
  - Willow (5.2.1)
  - Auth (1.44.1):
    - MissingDep (= 5.0.0)

DEPENDENCIES:
  - AzureMobile (~> 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb
  AzureData: f423992bd28e1006e3c358d3e3ce60d71f8ba090
  AzureMobile: 4fd580aa2f73f4a8ac463971b4a5483afd586f2a
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

COCOAPODS: 0.39.0";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(6);

        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureData", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureMobile", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "KeychainAccess", "3.2.1");
        this.AssertPodComponentNameAndVersion(detectedComponents, "Willow", "5.2.1");
        this.AssertPodComponentNameAndVersion(detectedComponents, "Auth", "1.44.1");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizeSubspecsAsSinglePodComponentAsync()
    {
        var podfileLockContent = @"PODS:
  - MSAL/app-lib (1.0.7)
  - MSAL/extension (1.0.7)
  - MSGraphClientSDK (1.0.0):
    - MSGraphClientSDK/Authentication (= 1.0.0)
    - MSGraphClientSDK/Common (= 1.0.0)
  - MSGraphClientSDK/Authentication (1.0.0)
  - MSGraphClientSDK/Common (1.0.0):
    - MSGraphClientSDK/Authentication

DEPENDENCIES:
  - MSAL
  - MSGraphClientSDK

SPEC CHECKSUMS:
  MSAL: e4c1cbcf59e04073b427ce9fbfc0346b54abb62e
  MSGraphClientSDK: ffc07a58a838e0702c7bf2a856367035d4a335d7

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        this.AssertPodComponentNameAndVersion(detectedComponents, "MSAL", "1.0.7");
        this.AssertPodComponentNameAndVersion(detectedComponents, "MSGraphClientSDK", "1.0.0");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizeGitComponentsAsync()
    {
        var podfileLockContent = @"PODS:
  - MSGraphClientSDK (1.0.0):
    - MSGraphClientSDK/Authentication (= 1.0.0)
    - MSGraphClientSDK/Common (= 1.0.0)
  - MSGraphClientSDK/Authentication (1.0.0)
  - MSGraphClientSDK/Common (1.0.0):
    - MSGraphClientSDK/Authentication

DEPENDENCIES:
  - MSGraphClientSDK (from `https://github.com/microsoftgraph/msgraph-sdk-objc.git`, branch `main`)

EXTERNAL SOURCES:
  MSGraphClientSDK:
    :branch: main
    :git: https://github.com/microsoftgraph/msgraph-sdk-objc.git

CHECKOUT OPTIONS:
  MSGraphClientSDK:
    :commit: da7223e3c455fe558de361c611df36c6dcc4229d
    :git: https://github.com/microsoftgraph/msgraph-sdk-objc.git

SPEC CHECKSUMS:
  MSGraphClientSDK: ffc07a58a838e0702c7bf2a856367035d4a335d7

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        this.AssertGitComponentHashAndUrl(detectedComponents, "da7223e3c455fe558de361c611df36c6dcc4229d", "https://github.com/microsoftgraph/msgraph-sdk-objc.git");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizeGitComponentsWithTagsAsPodComponentsAsync()
    {
        var podfileLockContent = @"PODS:
  - MSGraphClientSDK (1.0.0):
    - MSGraphClientSDK/Authentication (= 1.0.0)
    - MSGraphClientSDK/Common (= 1.0.0)
  - MSGraphClientSDK/Authentication (1.0.0)
  - MSGraphClientSDK/Common (1.0.0):
    - MSGraphClientSDK/Authentication

DEPENDENCIES:
  - MSGraphClientSDK (from `https://github.com/microsoftgraph/msgraph-sdk-objc.git`, tag `1.0.0`)

EXTERNAL SOURCES:
  MSGraphClientSDK:
    :branch: main
    :git: https://github.com/microsoftgraph/msgraph-sdk-objc.git

CHECKOUT OPTIONS:
  MSGraphClientSDK:
    :git: https://github.com/microsoftgraph/msgraph-sdk-objc.git
    :tag: 1.0.0

SPEC CHECKSUMS:
  MSGraphClientSDK: ffc07a58a838e0702c7bf2a856367035d4a335d7

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        this.AssertPodComponentNameAndVersion(detectedComponents, "MSGraphClientSDK", "1.0.0");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizeGitComponentsWithTagsAsPodComponents_GitUriAsync()
    {
        var podfileLockContent = @"PODS:
  - MSGraphClientSDK (1.0.0):
    - MSGraphClientSDK/Authentication (= 1.0.0)
    - MSGraphClientSDK/Common (= 1.0.0)
  - MSGraphClientSDK/Authentication (1.0.0)
  - MSGraphClientSDK/Common (1.0.0):
    - MSGraphClientSDK/Authentication

DEPENDENCIES:
  - MSGraphClientSDK (from `git@github.com/microsoftgraph/msgraph-sdk-objc.git`, tag `1.0.0`)

EXTERNAL SOURCES:
  MSGraphClientSDK:
    :branch: main
    :git: git@github.com/microsoftgraph/msgraph-sdk-objc.git

CHECKOUT OPTIONS:
  MSGraphClientSDK:
    :git: git@github.com/microsoftgraph/msgraph-sdk-objc.git
    :tag: 1.0.0

SPEC CHECKSUMS:
  MSGraphClientSDK: ffc07a58a838e0702c7bf2a856367035d4a335d7

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        this.AssertPodComponentNameAndVersion(detectedComponents, "MSGraphClientSDK", "1.0.0");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizePodComponentsFromExternalPodspecsAsync()
    {
        var podfileLockContent = @"PODS:
  - CocoaLumberjack (3.6.0):
    - CocoaLumberjack/Core (= 3.6.0)
  - CocoaLumberjack/Core (3.6.0)
  - SVGKit (2.1.0):
    - CocoaLumberjack (~> 3.0)

EXTERNAL SOURCES:
  SVGKit:
    :podspec: ""https://example.com/SVGKit.podspec""

DEPENDENCIES:
  - SVGKit (from `https://example.com/SVGKit.podspec`)

SPEC CHECKSUMS:
  CocoaLumberjack: 78b0c238666f4f58db069738ec176f4519557516
  SVGKit: 8a2fc74258bdb2abb54d3b65f3dd68b0277a9c4d

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        this.AssertPodComponentNameAndVersion(detectedComponents, "CocoaLumberjack", "3.6.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "SVGKit", "2.1.0");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizePodComponentsFromLocalPathAsync()
    {
        var podfileLockContent = @"PODS:
  - Keys (1.0.1)

EXTERNAL SOURCES:
  Keys:
    :path: Pods/CocoaPodsKeys

DEPENDENCIES:
  - Keys (from `Pods/CocoaPodsKeys`)

SPEC CHECKSUMS:
  Keys: a576f4c9c1c641ca913a959a9c62ed3f215a8de9

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        this.AssertPodComponentNameAndVersion(detectedComponents, "Keys", "1.0.1");
    }

    [TestMethod]
    public async Task TestPodDetector_MultiplePodfileLocksAsync()
    {
        var podfileLockContent = @"PODS:
  - AzureCore (0.5.0):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - AzureData (0.5.0):
    - AzureCore (= 0.5.0)
  - AzureMobile (0.5.0):
    - AzureData (= 0.5.0)
  - KeychainAccess (3.2.1)
  - Willow (5.2.1)

DEPENDENCIES:
  - AzureMobile (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb
  AzureData: f423992bd28e1006e3c358d3e3ce60d71f8ba090
  AzureMobile: 4fd580aa2f73f4a8ac463971b4a5483afd586f2a
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

COCOAPODS: 0.39.0";

        var podfileLockContent2 = @"PODS:
  - AzureCore (0.5.1):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - CocoaLumberjack (3.6.0):
    - CocoaLumberjack/Core (= 3.6.0)
  - CocoaLumberjack/Core (3.6.0)
  - KeychainAccess (3.2.1)
  - SVGKit (2.1.0):
    - CocoaLumberjack (~> 3.0)
  - Willow (5.2.1)

DEPENDENCIES:
  - SVGKit (~> 2.0)
  - AzureCore (= 0.5.1)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb
  CocoaLumberjack: 78b0c238666f4f58db069738ec176f4519557516
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  SVGKit: 8a2fc74258bdb2abb54d3b65f3dd68b0277a9c4d
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .WithFile("Podfile.lock", podfileLockContent2)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(8);

        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.1");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureData", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureMobile", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "CocoaLumberjack", "3.6.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "KeychainAccess", "3.2.1");
        this.AssertPodComponentNameAndVersion(detectedComponents, "SVGKit", "2.1.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "Willow", "5.2.1");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorSupportsDependencyRootsAsync()
    {
        var podfileLockContent = @"PODS:
  - AzureCore (0.5.0):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - AzureData (0.5.0):
    - AzureCore (= 0.5.0)
  - AzureMobile (0.5.0):
    - AzureData (= 0.5.0)
  - KeychainAccess (3.2.1)
  - Willow (5.2.1)

DEPENDENCIES:
  - AzureData (= 0.5.0)
  - AzureMobile (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb
  AzureData: f423992bd28e1006e3c358d3e3ce60d71f8ba090
  AzureMobile: 4fd580aa2f73f4a8ac463971b4a5483afd586f2a
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

COCOAPODS: 0.39.0";

        var podfileLockContent2 = @"PODS:
  - AzureCore (0.5.1):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - CocoaLumberjack (3.6.0):
    - CocoaLumberjack/Core (= 3.6.0)
  - CocoaLumberjack/Core (3.6.0)
  - KeychainAccess (3.2.1)
  - SVGKit (2.1.0):
    - CocoaLumberjack (~> 3.0)
  - Willow (5.2.1)

DEPENDENCIES:
  - SVGKit (from `https://github.com/SVGKit/SVGKit.git`, branch `2.x`)
  - AzureCore (= 0.5.1)

EXTERNAL SOURCES:
  SVGKit:
    :branch: 2.x
    :git: https://github.com/SVGKit/SVGKit.git

CHECKOUT OPTIONS:
  SVGKit:
    :commit: 0d4db53890c664fb8605666e6fbccd14912ff821
    :git: https://github.com/SVGKit/SVGKit.git

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fc
  CocoaLumberjack: 78b0c238666f4f58db069738ec176f4519557516
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  SVGKit: 8a2fc74258bdb2abb54d3b65f3dd68b0277a9c4d
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .WithFile("Podfile.lock", podfileLockContent2)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(8);

        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.1");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureData", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureMobile", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "CocoaLumberjack", "3.6.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "KeychainAccess", "3.2.1");
        this.AssertGitComponentHashAndUrl(detectedComponents, "0d4db53890c664fb8605666e6fbccd14912ff821", "https://github.com/SVGKit/SVGKit.git");
        this.AssertPodComponentNameAndVersion(detectedComponents, "Willow", "5.2.1");

        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureCore", Version: "0.5.1"), root: (Name: "AzureCore", Version: "0.5.1"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureData", Version: "0.5.0"), root: (Name: "AzureData", Version: "0.5.0"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureMobile", Version: "0.5.0"), root: (Name: "AzureMobile", Version: "0.5.0"));
        this.AssertGitComponentHasGitComponentDependencyRoot(componentRecorder, component: (Commit: "0d4db53890c664fb8605666e6fbccd14912ff821", Repo: "https://github.com/SVGKit/SVGKit.git"), root: (Commit: "0d4db53890c664fb8605666e6fbccd14912ff821", Repo: "https://github.com/SVGKit/SVGKit.git"));

        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureCore", Version: "0.5.0"), root: (Name: "AzureData", Version: "0.5.0"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureData", Version: "0.5.0"), root: (Name: "AzureMobile", Version: "0.5.0"));
        this.AssertPodComponentHasGitComponentDependencyRoot(componentRecorder, component: (Name: "CocoaLumberjack", Version: "3.6.0"), root: (Commit: "0d4db53890c664fb8605666e6fbccd14912ff821", Repo: "https://github.com/SVGKit/SVGKit.git"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "KeychainAccess", Version: "3.2.1"), root: (Name: "AzureCore", Version: "0.5.1"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "KeychainAccess", Version: "3.2.1"), root: (Name: "AzureData", Version: "0.5.0"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "Willow", Version: "5.2.1"), root: (Name: "AzureCore", Version: "0.5.1"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "Willow", Version: "5.2.1"), root: (Name: "AzureData", Version: "0.5.0"));
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorSupportsDependencyRoots_GitUriAsync()
    {
        var podfileLockContent = @"PODS:
  - AzureCore (0.5.0):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - AzureData (0.5.0):
    - AzureCore (= 0.5.0)
  - AzureMobile (0.5.0):
    - AzureData (= 0.5.0)
  - KeychainAccess (3.2.1)
  - Willow (5.2.1)

DEPENDENCIES:
  - AzureData (= 0.5.0)
  - AzureMobile (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb
  AzureData: f423992bd28e1006e3c358d3e3ce60d71f8ba090
  AzureMobile: 4fd580aa2f73f4a8ac463971b4a5483afd586f2a
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

COCOAPODS: 0.39.0";

        var podfileLockContent2 = @"PODS:
  - AzureCore (0.5.1):
    - KeychainAccess (~> 3.2)
    - Willow (~> 5.2)
  - CocoaLumberjack (3.6.0):
    - CocoaLumberjack/Core (= 3.6.0)
  - CocoaLumberjack/Core (3.6.0)
  - KeychainAccess (3.2.1)
  - SVGKit (2.1.0):
    - CocoaLumberjack (~> 3.0)
  - Willow (5.2.1)

DEPENDENCIES:
  - SVGKit (from `git@github.com/SVGKit/SVGKit.git`, branch `2.x`)
  - AzureCore (= 0.5.1)

EXTERNAL SOURCES:
  SVGKit:
    :branch: 2.x
    :git: git@github.com/SVGKit/SVGKit.git

CHECKOUT OPTIONS:
  SVGKit:
    :commit: 0d4db53890c664fb8605666e6fbccd14912ff821
    :git: git@github.com/SVGKit/SVGKit.git

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fc
  CocoaLumberjack: 78b0c238666f4f58db069738ec176f4519557516
  KeychainAccess: d5470352939ced6d6f7fb51cb2e67aae51fc294f
  SVGKit: 8a2fc74258bdb2abb54d3b65f3dd68b0277a9c4d
  Willow: a6310f9aedcb6f4de8c35b94fd3416a660ae9280

PODFILE CHECKSUM: accace11c2720ac62a63c1b7629cc202a7e108b8

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .WithFile("Podfile.lock", podfileLockContent2)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(8);

        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.1");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureData", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureMobile", "0.5.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "CocoaLumberjack", "3.6.0");
        this.AssertPodComponentNameAndVersion(detectedComponents, "KeychainAccess", "3.2.1");
        this.AssertGitComponentHashAndUrl(detectedComponents, "0d4db53890c664fb8605666e6fbccd14912ff821", "https://github.com/SVGKit/SVGKit.git");
        this.AssertPodComponentNameAndVersion(detectedComponents, "Willow", "5.2.1");

        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureCore", Version: "0.5.1"), root: (Name: "AzureCore", Version: "0.5.1"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureData", Version: "0.5.0"), root: (Name: "AzureData", Version: "0.5.0"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureMobile", Version: "0.5.0"), root: (Name: "AzureMobile", Version: "0.5.0"));
        this.AssertGitComponentHasGitComponentDependencyRoot(componentRecorder, component: (Commit: "0d4db53890c664fb8605666e6fbccd14912ff821", Repo: "https://github.com/SVGKit/SVGKit.git"), root: (Commit: "0d4db53890c664fb8605666e6fbccd14912ff821", Repo: "https://github.com/SVGKit/SVGKit.git"));

        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureCore", Version: "0.5.0"), root: (Name: "AzureData", Version: "0.5.0"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "AzureData", Version: "0.5.0"), root: (Name: "AzureMobile", Version: "0.5.0"));
        this.AssertPodComponentHasGitComponentDependencyRoot(componentRecorder, component: (Name: "CocoaLumberjack", Version: "3.6.0"), root: (Commit: "0d4db53890c664fb8605666e6fbccd14912ff821", Repo: "https://github.com/SVGKit/SVGKit.git"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "KeychainAccess", Version: "3.2.1"), root: (Name: "AzureCore", Version: "0.5.1"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "KeychainAccess", Version: "3.2.1"), root: (Name: "AzureData", Version: "0.5.0"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "Willow", Version: "5.2.1"), root: (Name: "AzureCore", Version: "0.5.1"));
        this.AssertPodComponentHasPodComponentDependencyRoot(componentRecorder, component: (Name: "Willow", Version: "5.2.1"), root: (Name: "AzureData", Version: "0.5.0"));
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorHandlesMainSpecRepoDifferencesAsync()
    {
        var podfileLockContent = @"PODS:
  - AzureCore (0.5.0)

SPEC REPOS:
  https://github.com/cocoapods/specs.git:
    - AzureCore

DEPENDENCIES:
  - AzureCore (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb

COCOAPODS: 1.7.3";

        var podfileLockContent2 = @"PODS:
  - AzureCore (0.5.0)

SPEC REPOS:
  https://github.com/CocoaPods/Specs.git:
    - AzureCore

DEPENDENCIES:
  - AzureCore (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb

COCOAPODS: 1.8.4";

        var podfileLockContent3 = @"PODS:
  - AzureCore (0.5.0)

SPEC REPOS:
  trunk:
    - AzureCore

DEPENDENCIES:
  - AzureCore (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .WithFile("Podfile.lock", podfileLockContent2)
            .WithFile("Podfile.lock", podfileLockContent3)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        this.AssertPodComponentNameAndVersion(detectedComponents, "AzureCore", "0.5.0");
    }

    [TestMethod]
    public async Task TestPodDetector_DetectorRecognizeComponentsSpecRepoAsync()
    {
        var podfileLockContent = @"PODS:
  - AzureCore (0.5.0)

SPEC REPOS:
  trunk:
    - AzureCore

DEPENDENCIES:
  - AzureCore (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb

COCOAPODS: 1.8.4";

        var podfileLockContent2 = @"PODS:
  - AzureCore (0.5.0)

SPEC REPOS:
  https://msblox.visualstudio.com/DefaultCollection/_git/CocoaPods:
    - AzureCore

DEPENDENCIES:
  - AzureCore (= 0.5.0)

SPEC CHECKSUMS:
  AzureCore: 9f6c42e03d59a13b508bff356a85cd9438b654fb

COCOAPODS: 1.8.4";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Podfile.lock", podfileLockContent)
            .WithFile("Podfile.lock", podfileLockContent2, fileLocation: Path.Join(Path.GetTempPath(), "sub-folder", "Podfile.lock"))
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var firstComponent = detectedComponents.First();
        componentRecorder.ForOneComponent(firstComponent.Component.Id, grouping => grouping.AllFileLocations.Should().HaveCount(2));
    }

    private void AssertPodComponentNameAndVersion(IEnumerable<DetectedComponent> detectedComponents, string name, string version)
    {
        detectedComponents.SingleOrDefault(component =>
                component.Component is PodComponent &&
                (component.Component as PodComponent).Name.Equals(name) &&
                (component.Component as PodComponent).Version.Equals(version)).Should().NotBeNull(
            $"Component with name {name} and version {version} was not found");
    }

    private void AssertGitComponentHashAndUrl(IEnumerable<DetectedComponent> detectedComponents, string commitHash, string repositoryUrl)
    {
        detectedComponents.SingleOrDefault(component =>
                component.Component is GitComponent &&
                (component.Component as GitComponent).CommitHash.Equals(commitHash) &&
                (component.Component as GitComponent).RepositoryUrl.Equals(repositoryUrl)).Should().NotBeNull(
            $"Component with commit hash {commitHash} and repository url {repositoryUrl} was not found");
    }

    private void AssertPodComponentHasPodComponentDependencyRoot(IComponentRecorder recorder, (string Name, string Version) component, (string Name, string Version) root)
    {
        recorder.IsDependencyOfExplicitlyReferencedComponents<PodComponent>(
            new PodComponent(component.Name, component.Version).Id,
            x => x.Id == new PodComponent(root.Name, root.Version).Id).Should().BeTrue();
    }

    private void AssertPodComponentHasGitComponentDependencyRoot(IComponentRecorder recorder, (string Name, string Version) component, (string Commit, string Repo) root)
    {
        recorder.IsDependencyOfExplicitlyReferencedComponents<GitComponent>(
            new PodComponent(component.Name, component.Version).Id,
            x => x.Id == new GitComponent(new Uri(root.Repo), root.Commit).Id).Should().BeTrue();
    }

    private void AssertGitComponentHasGitComponentDependencyRoot(IComponentRecorder recorder, (string Commit, string Repo) component, (string Commit, string Repo) root)
    {
        recorder.IsDependencyOfExplicitlyReferencedComponents<GitComponent>(
            new GitComponent(new Uri(component.Repo), component.Commit).Id,
            x => x.Id == new GitComponent(new Uri(root.Repo), root.Commit).Id).Should().BeTrue();
    }
}

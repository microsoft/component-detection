#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Ruby;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RubyDetectorTest : BaseDetectorTest<RubyComponentDetector>
{
    public RubyDetectorTest()
    {
        var componentRecorder = new ComponentRecorder(enableManualTrackingOfExplicitReferences: false);
        this.DetectorTestUtility.WithScanRequest(
            new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                null,
                null,
                new Dictionary<string, string>(),
                null,
                componentRecorder));
    }

    [TestMethod]
    public async Task TestRubyDetector_TestMultipleLockfilesAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      actioncable (= 5.2.2.1)
    actioncable (5.2.1)
      nio4r (~> 2.0)
      websocket-driver (>= 0.6.1)
    faraday (1.0.0)
    nio4r (5.2.1)
    websocket-driver (0.6.1)

BUNDLED WITH
    1.17.2";

        var gemFileLockContent2 = @"GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      actioncable (= 5.2.2.1)
    actioncable (5.2.1)
      nio4r (~> 2.0)
      websocket-driver (>= 0.6.1)
    faraday (1.0.0)
    nio4r (5.2.1)
    websocket-driver (0.6.1)

BUNDLED WITH
    1.17.3";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .WithFile("2Gemfile.lock", gemFileLockContent2)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(7);

        this.AssertRubyComponentNameAndVersion(detectedComponents, "acme-client", "2.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "actioncable", "5.2.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "faraday", "1.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "nio4r", "5.2.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "websocket-driver", "0.6.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "bundler", "1.17.2");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "bundler", "1.17.3");
    }

    [TestMethod]
    public async Task TestRubyDetector_TestGemsWithUppercase_LockFileAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    CFPropertyList (3.0.4)
      rexml

BUNDLED WITH
    2.2.28";
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        // we do not record invalid/unknown versions
        this.AssertRubyComponentNameAndVersion(detectedComponents, "CFPropertyList", "3.0.4");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "bundler", "2.2.28");
    }

    [TestMethod]
    public async Task TestRubyDetector_DetectorParseWithBundlerVersionAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      actioncable (= 5.2.2.1)
    actioncable (5.2.1)
      nio4r (~> 2.0)
      websocket-driver (>= 0.6.1)
    faraday (1.0.0)
    nio4r (5.2.1)
    websocket-driver (0.6.1)

BUNDLED WITH
    1.17.3";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(6);

        this.AssertRubyComponentNameAndVersion(detectedComponents, "acme-client", "2.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "actioncable", "5.2.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "faraday", "1.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "nio4r", "5.2.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "websocket-driver", "0.6.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "bundler", "1.17.3");
    }

    [TestMethod]
    public async Task TestRubyDetector_DetectorRecognizeGemComponentsAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      actioncable (= 5.2.2.1)
    actioncable (5.2.1)
      nio4r (~> 2.0)
      websocket-driver (>= 0.6.1)
    faraday (1.0.0)
    nio4r (5.2.1)
    nokogiri (~> 1.8.2)
    websocket-driver (0.6.1)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(5);

        this.AssertRubyComponentNameAndVersion(detectedComponents, "acme-client", "2.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "actioncable", "5.2.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "faraday", "1.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "nio4r", "5.2.1");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "websocket-driver", "0.6.1");
    }

    [TestMethod]
    public async Task TestRubyDetector_ParentWithTildeInVersion_IsExcludedAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      nokogiri (~> 1.8.2)
    faraday (1.0.0)
    nokogiri (~> 1.8.2)
      mini_portile2 (~> 2.3.0)
    mini_portile2 (2.3.0)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        this.AssertRubyComponentNameAndVersion(detectedComponents, "acme-client", "2.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "faraday", "1.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, "mini_portile2", "2.3.0");
    }

    [TestMethod]
    public async Task TestRubyDetector_DetectorCreatesADependencyGraphAsync()
    {
        var gemFileLockContent = @"GIT
  remote: https://github.com/mikel/mail.git
  revision: 3204c0b4733166b9664a552006286227dea09953
  branch: 2-7-stable
  specs:
    mail (2.7.2.edge)
      websocket-driver (>= 0.6.1)

GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      actioncable (= 5.2.2.1)
    actioncable (5.2.1)
      nio4r (~> 2.0)
    faraday (1.0.0)
    nio4r (5.2.1)
    websocket-driver (0.6.1)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.Single();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var acmeClientComponentId = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("acme-client")).Component.Id;
        var faradayComponentId = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("faraday")).Component.Id;
        var actioncableComponentId = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("actioncable")).Component.Id;
        var nior4rComponentId = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("nio4r")).Component.Id;
        var websocketDriverComponentId = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("websocket-driver")).Component.Id;
        var mailComponentId = detectedComponents.Single(c => c.Component is GitComponent component && component.CommitHash.Equals("3204c0b4733166b9664a552006286227dea09953")).Component.Id;

        var acmeClientDependencies = dependencyGraph.GetDependenciesForComponent(acmeClientComponentId);
        acmeClientDependencies.Should().HaveCount(2);
        acmeClientDependencies.Should().Contain(dep => dep == faradayComponentId);
        acmeClientDependencies.Should().Contain(dep => dep == actioncableComponentId);

        var actionCableDependencies = dependencyGraph.GetDependenciesForComponent(actioncableComponentId);
        actionCableDependencies.Should().ContainSingle();
        actionCableDependencies.Should().Contain(dep => dep == nior4rComponentId);

        var faradayDependencies = dependencyGraph.GetDependenciesForComponent(faradayComponentId);
        faradayDependencies.Should().BeEmpty();

        var niorDependencies = dependencyGraph.GetDependenciesForComponent(nior4rComponentId);
        niorDependencies.Should().BeEmpty();

        var websocketDependencies = dependencyGraph.GetDependenciesForComponent(websocketDriverComponentId);
        websocketDependencies.Should().BeEmpty();

        var mailComponentDependencies = dependencyGraph.GetDependenciesForComponent(mailComponentId);
        mailComponentDependencies.Should().ContainSingle();
        mailComponentDependencies.Should().Contain(dep => dep == websocketDriverComponentId);
    }

    [TestMethod]
    public async Task TestRubyDetector_ComponentsRootsAreFilledCorrectlyAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    acme-client (2.0.0)
      faraday (~> 0.9, >= 0.9.1)
      actioncable (= 5.2.2.1)
    actioncable (5.2.1)
      nio4r (~> 2.0)
    faraday (1.0.0)
    nio4r (5.2.1)
    websocket-driver (0.6.1)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        var acmeClientComponent = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("acme-client"));
        var faradayComponent = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("faraday"));
        var actioncableComponent = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("actioncable"));
        var nior4rComponent = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("nio4r"));
        var websocketDriverComponent = detectedComponents.Single(c => c.Component is RubyGemsComponent component && component.Name.Equals("websocket-driver"));

        componentRecorder.AssertAllExplicitlyReferencedComponents<RubyGemsComponent>(
            acmeClientComponent.Component.Id,
            parentComponent => parentComponent.Id == acmeClientComponent.Component.Id);

        componentRecorder.AssertAllExplicitlyReferencedComponents<RubyGemsComponent>(
            faradayComponent.Component.Id,
            parentComponent => parentComponent.Id == acmeClientComponent.Component.Id);

        componentRecorder.AssertAllExplicitlyReferencedComponents<RubyGemsComponent>(
            actioncableComponent.Component.Id,
            parentComponent => parentComponent.Id == acmeClientComponent.Component.Id);

        componentRecorder.AssertAllExplicitlyReferencedComponents<RubyGemsComponent>(
            nior4rComponent.Component.Id,
            parentComponent => parentComponent.Id == acmeClientComponent.Component.Id);

        componentRecorder.AssertAllExplicitlyReferencedComponents<RubyGemsComponent>(
            websocketDriverComponent.Component.Id,
            parentComponent => parentComponent.Id == websocketDriverComponent.Component.Id);
    }

    [TestMethod]
    public async Task TestRubyDetector_DetectorRecognizeGitComponentsAsync()
    {
        var gemFileLockContent = @"GIT
  remote: https://github.com/test/abc.git
  revision: commit-hash-1
  branch: 2-7-stable
  specs:
    abc (2.7.2.edge)

GIT
  remote: https://github.com/mikel/mail.git
  revision: commit-hash-2
  branch: 2-7-stable
  specs:
    mail (2.7.2.edge)
      mini_mime (>= 0.1.1)

GEM
  remote: https://rubygems.org/
  specs:
    mini_mime (2.0.0)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        this.AssertGitComponentHashAndUrl(detectedComponents, commitHash: "commit-hash-1", repositoryUrl: "https://github.com/test/abc.git");
        this.AssertGitComponentHashAndUrl(detectedComponents, commitHash: "commit-hash-2", repositoryUrl: "https://github.com/mikel/mail.git");
        this.AssertRubyComponentNameAndVersion(detectedComponents, name: "mini_mime", version: "2.0.0");
    }

    [TestMethod]
    public async Task TestRubyDetector_DetectorRecognizeParentChildRelationshipInGitComponentsAsync()
    {
        var gemFileLockContent = @"GIT
  remote: https://github.com/test/abc.git
  revision: commit-hash-1
  branch: 2-7-stable
  specs:
    abc (2.7.2.edge)
      mail (2.7.2.edge)

GIT
  remote: https://github.com/mikel/mail.git
  revision: commit-hash-2
  branch: 2-7-stable
  specs:
    mail (2.7.2.edge)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        this.AssertGitComponentAsRootAndGitComponentAsSubDependency(componentRecorder, rootHash: "commit-hash-1", subDependencyHash: "commit-hash-2");
    }

    [TestMethod]
    public async Task TestRubyDetector_DetectorRecognizeLocalDependenciesAsync()
    {
        var gemFileLockContent = @"GEM
  remote: https://rubygems.org/
  specs:
    mini_mime (2.0.0)

PATH
  remote: C:/test
  specs:
    test (1.0.0)

PATH
  remote: C:/test
  specs:
    test2 (1.0.0)";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("1Gemfile.lock", gemFileLockContent)
            .ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        this.AssertRubyComponentNameAndVersion(detectedComponents, name: "mini_mime", version: "2.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, name: "test", version: "1.0.0");
        this.AssertRubyComponentNameAndVersion(detectedComponents, name: "test2", version: "1.0.0");
    }

    private void AssertRubyComponentNameAndVersion(IEnumerable<DetectedComponent> detectedComponents, string name, string version)
    {
        detectedComponents.SingleOrDefault(c =>
                c.Component is RubyGemsComponent component &&
                component.Name.Equals(name) &&
                component.Version.Equals(version)).Should().NotBeNull(
            $"Component with name {name} and version {version} was not found");
    }

    private void AssertGitComponentHashAndUrl(IEnumerable<DetectedComponent> detectedComponents, string commitHash, string repositoryUrl)
    {
        detectedComponents.SingleOrDefault(c =>
            c.Component is GitComponent component &&
            component.CommitHash.Equals(commitHash) &&
            component.RepositoryUrl.Equals(repositoryUrl)).Should().NotBeNull();
    }

    private void AssertGitComponentAsRootAndGitComponentAsSubDependency(IComponentRecorder recorder, string rootHash, string subDependencyHash)
    {
        var childDep = recorder.GetDetectedComponents().First(x => (x.Component as GitComponent)?.CommitHash == subDependencyHash);
        recorder.IsDependencyOfExplicitlyReferencedComponents<GitComponent>(
            childDep.Component.Id,
            parent => parent.CommitHash == rootHash).Should().BeTrue();
    }
}

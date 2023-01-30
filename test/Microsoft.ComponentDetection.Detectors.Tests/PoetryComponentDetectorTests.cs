namespace Microsoft.ComponentDetection.Detectors.Tests;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Poetry;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PoetryComponentDetectorTests
{
    private DetectorTestUtility<PoetryComponentDetector> detectorTestUtility;

    [TestInitialize]
    public void TestInitialize()
    {
        this.detectorTestUtility = DetectorTestUtilityCreator.Create<PoetryComponentDetector>();
    }

    [TestMethod]
    public async Task TestPoetryDetector_TestCustomSourceAsync()
    {
        var poetryLockContent = @"[[package]]
name = ""certifi""
version = ""2021.10.8""
description = ""Python package for providing Mozilla's CA Bundle.""
category = ""main""
optional = false
python-versions = ""*""

[package.source]
type = ""legacy""
url = ""https://pypi.custom.com//simple""
reference = ""custom""
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("poetry.lock", poetryLockContent)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(1, detectedComponents.Count());

        AssertPipComponentNameAndVersion(detectedComponents, "certifi", "2021.10.8");
        var queryString = detectedComponents.Single(component => ((PipComponent)component.Component).Name.Contains("certifi"));
        Assert.IsFalse(componentRecorder.GetEffectiveDevDependencyValue(queryString.Component.Id).GetValueOrDefault(false));
    }

    [TestMethod]
    public async Task TestPoetryDetector_TestDevDependencyAsync()
    {
        var poetryLockContent = @"[[package]]
name = ""certifi""
version = ""2021.10.8""
description = ""Python package for providing Mozilla's CA Bundle.""
category = ""dev""
optional = false
python-versions = ""*""
";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("poetry.lock", poetryLockContent)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(1, detectedComponents.Count());

        AssertPipComponentNameAndVersion(detectedComponents, "certifi", "2021.10.8");

        var queryString = detectedComponents.Single(component => ((PipComponent)component.Component).Name.Contains("certifi"));
        Assert.IsTrue(componentRecorder.GetEffectiveDevDependencyValue(queryString.Component.Id).GetValueOrDefault(false));
    }

    [TestMethod]
    public async Task TestPoetryDetector_TestGitDependencyAsync()
    {
        var poetryLockContent = @"[[package]]
name = ""certifi""
version = ""2021.10.8""
description = ""Python package for providing Mozilla's CA Bundle.""
category = ""dev""
optional = false
python-versions = ""*""

[[package]]
name = ""requests""
version = ""2.26.0""
description = ""Python HTTP for Humans.""
category = ""main""
optional = false
python-versions = "">=2.7, !=3.0.*, !=3.1.*, !=3.2.*, !=3.3.*, !=3.4.*, !=3.5.*""
develop = false

[package.dependencies]
certifi = "">=2017.4.17""
charset-normalizer = {version = "">=2.0.0,<2.1.0"", markers = ""python_version >= \""3\""""}
idna = {version = "">=2.5,<4"", markers = ""python_version >= \""3\""""}
urllib3 = "">=1.21.1,<1.27""

[package.extras]
socks = [""PySocks (>=1.5.6,!=1.5.7)"", ""win-inet-pton""]
use_chardet_on_py3 = [""chardet (>=3.0.2,<5)""]

[package.source]
type = ""git""
url = ""https://github.com/requests/requests.git""
reference = ""master""
resolved_reference = ""232a5596424c98d11c3cf2e29b2f6a6c591c2ff3""";

        var (scanResult, componentRecorder) = await this.detectorTestUtility
            .WithFile("poetry.lock", poetryLockContent)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(2, detectedComponents.Count());

        AssertGitComponentHashAndUrl(detectedComponents, "232a5596424c98d11c3cf2e29b2f6a6c591c2ff3", "https://github.com/requests/requests.git");
    }

    private static void AssertPipComponentNameAndVersion(IEnumerable<DetectedComponent> detectedComponents, string name, string version)
    {
        Assert.IsNotNull(
            detectedComponents.SingleOrDefault(c =>
                c.Component is PipComponent component &&
                component.Name.Equals(name) &&
                component.Version.Equals(version)),
            $"Component with name {name} and version {version} was not found");
    }

    private static void AssertGitComponentHashAndUrl(IEnumerable<DetectedComponent> detectedComponents, string commitHash, string repositoryUrl)
    {
        Assert.IsNotNull(detectedComponents.SingleOrDefault(c =>
            c.Component is GitComponent component &&
            component.CommitHash.Equals(commitHash) &&
            component.RepositoryUrl.Equals(repositoryUrl)));
    }
}

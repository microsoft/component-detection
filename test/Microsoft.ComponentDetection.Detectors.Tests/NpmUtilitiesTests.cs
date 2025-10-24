#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NpmUtilitiesTests
{
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.loggerMock = new Mock<ILogger>();
    }

    [TestMethod]
    public void TestGetTypedComponent()
    {
        var json = @"{
                ""async"": {
                    ""version"": ""2.3.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                },
            }";

        var j = JObject.Parse(json);

        var componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);

        componentFromJProperty.Should().NotBeNull();
        componentFromJProperty.Type.Should().Be(ComponentType.Npm);

        var npmComponent = (NpmComponent)componentFromJProperty;
        npmComponent.Name.Should().Be("async");
        npmComponent.Version.Should().Be("2.3.0");
    }

    [TestMethod]
    public void TestGetTypedComponent_FailsOnMalformed()
    {
        var json = @"{
                ""async"": {
                    ""version"": ""NOTAVERSION"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                },
            }";

        var j = JObject.Parse(json);

        var componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);

        componentFromJProperty.Should().BeNull();
    }

    [TestMethod]
    public void TestGetTypedComponent_FailsOnInvalidPackageName()
    {
        var jsonInvalidCharacter = @"{
                ""async<"": {
                    ""version"": ""1.0.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                },
            }";

        var j = JObject.Parse(jsonInvalidCharacter);
        var componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);
        componentFromJProperty.Should().BeNull();

        var jsonUrlName = @"{
                ""http://thisis/my/packagename"": {
                    ""version"": ""1.0.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                },
            }";

        j = JObject.Parse(jsonUrlName);
        componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);
        componentFromJProperty.Should().BeNull();

        var jsonInvalidInitialCharacter1 = @"{
                ""_async"": {
                    ""version"": ""1.0.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                },
            }";

        j = JObject.Parse(jsonInvalidInitialCharacter1);
        componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);
        componentFromJProperty.Should().BeNull();

        var jsonInvalidInitialCharacter2 = @"{
                "".async"": {
                    ""version"": ""1.0.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                },
            }";

        j = JObject.Parse(jsonInvalidInitialCharacter2);
        componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);
        componentFromJProperty.Should().BeNull();

        var longPackageName = new string('a', 214);
        var jsonLongName = $@"{{
                ""{longPackageName}"": {{
                    ""version"": ""1.0.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k=""
                }},
            }}";

        j = JObject.Parse(jsonLongName);
        componentFromJProperty = NpmComponentUtilities.GetTypedComponent(j.Children<JProperty>().Single(), "registry.npmjs.org", this.loggerMock.Object);
        componentFromJProperty.Should().BeNull();
    }

    [TestMethod]
    public void TestTryParseNpmVersion()
    {
        var parsed = NpmComponentUtilities.TryParseNpmVersion("registry.npmjs.org", "archiver", "https://registry.npmjs.org/archiver-2.1.1.tgz", out var parsedVersion);
        parsed.Should().BeTrue();
        parsedVersion.ToString().Should().Be("2.1.1");

        parsed = NpmComponentUtilities.TryParseNpmVersion("registry.npmjs.org", "archiver", "notavalidurl", out parsedVersion);
        parsed.Should().BeFalse();
    }

    [TestMethod]
    public void TestTraverseAndGetRequirementsAndDependencies()
    {
        var json = @"{
                ""archiver"": {
                    ""version"": ""2.3.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                    ""dependencies"": {
                            ""archiver-utils"": {
                                ""version"": ""1.3.0"",
                                ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/archiver-utils/-/archiver-utils-1.3.0.tgz"",
                                ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                            }
                    }
                },
            }";

        var jsonChildren = JObject.Parse(json).Children<JProperty>();
        var currentDependency = jsonChildren.Single();
        var dependencyLookup = jsonChildren.ToDictionary(dependency => dependency.Name);

        var typedComponent = NpmComponentUtilities.GetTypedComponent(currentDependency, "registry.npmjs.org", this.loggerMock.Object);
        var componentRecorder = new ComponentRecorder();

        var singleFileComponentRecorder1 = componentRecorder.CreateSingleFileComponentRecorder("/this/is/a/test/path/");
        var singleFileComponentRecorder2 = componentRecorder.CreateSingleFileComponentRecorder("/this/is/a/different/path/");

        NpmComponentUtilities.TraverseAndRecordComponents(currentDependency, singleFileComponentRecorder1, typedComponent, typedComponent);
        NpmComponentUtilities.TraverseAndRecordComponents(currentDependency, singleFileComponentRecorder2, typedComponent, typedComponent);

        componentRecorder.GetDetectedComponents().Should().ContainSingle();
        componentRecorder.GetComponent(typedComponent.Id).Should().NotBeNull();

        var graph1 = componentRecorder.GetDependencyGraphsByLocation()["/this/is/a/test/path/"];
        var graph2 = componentRecorder.GetDependencyGraphsByLocation()["/this/is/a/different/path/"];

        graph1.GetExplicitReferencedDependencyIds(typedComponent.Id).Should().Contain(typedComponent.Id);
        graph2.GetExplicitReferencedDependencyIds(typedComponent.Id).Should().Contain(typedComponent.Id);
        componentRecorder.GetEffectiveDevDependencyValue(typedComponent.Id).GetValueOrDefault(true).Should().BeFalse();

        var json1 = @"{
                ""test"": {
                    ""version"": ""2.0.0"",
                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/async/-/async-2.3.0.tgz"",
                    ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                    ""dev"": ""true""
                },
            }";

        var jsonChildren1 = JObject.Parse(json1).Children<JProperty>();
        var currentDependency1 = jsonChildren1.Single();
        var dependencyLookup1 = jsonChildren1.ToDictionary(dependency => dependency.Name);

        var typedComponent1 = NpmComponentUtilities.GetTypedComponent(currentDependency1, "registry.npmjs.org", this.loggerMock.Object);

        NpmComponentUtilities.TraverseAndRecordComponents(currentDependency1, singleFileComponentRecorder2, typedComponent1, typedComponent1);

        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        graph2.GetExplicitReferencedDependencyIds(typedComponent1.Id).Should().Contain(typedComponent1.Id);
        componentRecorder.GetEffectiveDevDependencyValue(typedComponent1.Id).GetValueOrDefault(false).Should().BeTrue();

        NpmComponentUtilities.TraverseAndRecordComponents(currentDependency1, singleFileComponentRecorder2, typedComponent, typedComponent1, parentComponentId: typedComponent1.Id);

        componentRecorder.GetDetectedComponents().Should().HaveCount(2);
        var explicitlyReferencedDependencyIds = graph2.GetExplicitReferencedDependencyIds(typedComponent.Id);
        explicitlyReferencedDependencyIds.Should().Contain(typedComponent.Id);
        explicitlyReferencedDependencyIds.Should().Contain(typedComponent1.Id);
        explicitlyReferencedDependencyIds.Should().HaveCount(2);
    }

    [TestMethod]
    public void AddOrUpdateDetectedComponent_NewComponent_ComponentAdded()
    {
        var expectedDetectedComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"));
        var expectedDetectedDevComponent = new DetectedComponent(new NpmComponent("test2", "1.0.0"));

        var componentRecorder = new ComponentRecorder();

        var addedComponent1 = NpmComponentUtilities.AddOrUpdateDetectedComponent(
            componentRecorder.CreateSingleFileComponentRecorder("path1"), expectedDetectedComponent.Component, isDevDependency: false);

        var addedComponent2 = NpmComponentUtilities.AddOrUpdateDetectedComponent(
            componentRecorder.CreateSingleFileComponentRecorder("path2"), expectedDetectedDevComponent.Component, isDevDependency: true);

        addedComponent1.Should().BeEquivalentTo(expectedDetectedComponent, options => options.Excluding(obj => obj.DependencyRoots));
        addedComponent2.Should().BeEquivalentTo(expectedDetectedDevComponent, options => options.Excluding(obj => obj.DependencyRoots));

        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        var nonDevComponent = componentRecorder.GetComponent(expectedDetectedComponent.Component.Id);
        nonDevComponent.Should().BeEquivalentTo(expectedDetectedComponent.Component);
        componentRecorder.GetEffectiveDevDependencyValue(nonDevComponent.Id).Should().Be(false);
        componentRecorder.ForOneComponent(nonDevComponent.Id, grouping => grouping.AllFileLocations.Should().BeEquivalentTo("path1"));

        var devComponent = componentRecorder.GetComponent(expectedDetectedDevComponent.Component.Id);
        devComponent.Should().BeEquivalentTo(expectedDetectedDevComponent.Component);
        componentRecorder.GetEffectiveDevDependencyValue(devComponent.Id).Should().Be(true);
        componentRecorder.ForOneComponent(devComponent.Id, grouping => grouping.AllFileLocations.Should().BeEquivalentTo("path2"));
    }

    [TestMethod]

    public void AddOrUpdateDetectedComponent_ComponentExistAsDevDependencyNewUpdateIsNoDevDependency_DevDependencyIsUpdatedToFalse()
    {
        var detectedComponent = new DetectedComponent(new NpmComponent("name", "1.0"))
        {
            DevelopmentDependency = true,
        };

        var componentRecorder = new ComponentRecorder(new Mock<ILogger>().Object);
        var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("path");
        singleFileComponentRecorder.RegisterUsage(detectedComponent);

        var updatedDetectedComponent = NpmComponentUtilities.AddOrUpdateDetectedComponent(
            componentRecorder.CreateSingleFileComponentRecorder("path"), detectedComponent.Component, isDevDependency: false);

        componentRecorder.GetEffectiveDevDependencyValue(detectedComponent.Component.Id).Should().BeFalse();
        componentRecorder.GetEffectiveDevDependencyValue(updatedDetectedComponent.Component.Id).Should().BeFalse();
    }

    [TestMethod]
    public void GetModuleName_ReturnsAsExpected()
    {
        var testCases = new[]
        {
            ("test", "test"), ("@types/test", "@types/test"), ("node_modules/test", "test"),
            ("node_modules/@types/test", "@types/test"), ("node_modules/root/node_modules/test", "test"),
            ("node_modules/root/node_modules/@types/test", "@types/test"),
            ("node_modules/rootA/node_modules/rootB/node_modules/test", "test"),
            ("node_modules/rootA/node_modules/rootB/node_modules/@types/test", "@types/test"),
        };

        foreach (var (path, expectedModuleName) in testCases)
        {
            NpmComponentUtilities.GetModuleName(path).Should().Be(expectedModuleName);
        }
    }
}

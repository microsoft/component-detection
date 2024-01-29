namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.ComponentDetection.Detectors.Maven.MavenParsingUtilities;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MavenParsingUtilitiesTests
{
    [TestMethod]
    public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_HappyPath()
    {
        var componentAndMetaData =
            GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT:provided");

        componentAndMetaData.Should().NotBeNull();
        componentAndMetaData.Component.Should().NotBeNull();
        componentAndMetaData.IsDevelopmentDependency.Should().NotBeNull();
        componentAndMetaData.DependencyScope.Should().NotBeNull();

        var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
        actualComponent.Should().BeOfType<MavenComponent>();

        var expectedComponent = new MavenComponent("org.apache.maven", "maven-artifact", "3.6.1-SNAPSHOT");

        actualComponent.ArtifactId.Should().Be(expectedComponent.ArtifactId);
        actualComponent.GroupId.Should().Be(expectedComponent.GroupId);
        actualComponent.Version.Should().Be(expectedComponent.Version);

        componentAndMetaData.IsDevelopmentDependency.Should().BeFalse();
        componentAndMetaData.DependencyScope.Should().Be(DependencyScope.MavenProvided);
    }

    [TestMethod]
    public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_DefaultScopeCompile()
    {
        var componentAndMetaData =
            GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT");

        componentAndMetaData.Should().NotBeNull();
        componentAndMetaData.DependencyScope.Should().NotBeNull();

        var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
        actualComponent.Should().BeOfType<MavenComponent>();
        componentAndMetaData.DependencyScope.Should().Be(DependencyScope.MavenCompile);
    }

    [TestMethod]
    public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_DiscardLeftoverStringWhileParsingScope()
    {
        var componentAndMetaData =
            GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT:provided (optional)");

        componentAndMetaData.Should().NotBeNull();
        componentAndMetaData.DependencyScope.Should().NotBeNull();

        var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
        actualComponent.Should().BeOfType<MavenComponent>();
        componentAndMetaData.DependencyScope.Should().Be(DependencyScope.MavenProvided);
    }

    [TestMethod]
    public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_DevelopmentDependencyTrue()
    {
        var componentAndMetaData =
            GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT:test");

        componentAndMetaData.Should().NotBeNull();
        componentAndMetaData.IsDevelopmentDependency.Should().NotBeNull();

        var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
        actualComponent.Should().BeOfType<MavenComponent>();
        componentAndMetaData.IsDevelopmentDependency.Should().BeTrue();
    }

    [TestMethod]
    public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_InvalidScope()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT:invalidScope"));
        ex.Message.Contains("invalid scope", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.ComponentDetection.Detectors.Maven;
using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.ComponentDetection.Detectors.Maven.MavenParsingUtilities;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using FluentAssertions.Collections;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
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

            Assert.IsNotNull(componentAndMetaData);
            Assert.IsNotNull(componentAndMetaData.Component);
            Assert.IsNotNull(componentAndMetaData.IsDevelopmentDependency);
            Assert.IsNotNull(componentAndMetaData.dependencyScope);
            
            var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
            Assert.IsInstanceOfType(actualComponent, typeof(MavenComponent));

            var expectedComponent = new MavenComponent("org.apache.maven", "maven-artifact", "3.6.1-SNAPSHOT");
            
            Assert.AreEqual(expectedComponent.ArtifactId, actualComponent.ArtifactId);
            Assert.AreEqual(expectedComponent.GroupId, actualComponent.GroupId);
            Assert.AreEqual(expectedComponent.Version, actualComponent.Version);

            Assert.IsFalse(componentAndMetaData.IsDevelopmentDependency);
            Assert.AreEqual(DependencyScope.MavenProvided, componentAndMetaData.dependencyScope);
        }

        [TestMethod]
        public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_DefaultScopeCompile()
        {
            var componentAndMetaData =
                GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT");

            Assert.IsNotNull(componentAndMetaData);
            Assert.IsNotNull(componentAndMetaData.dependencyScope);

            var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
            Assert.IsInstanceOfType(actualComponent, typeof(MavenComponent));
            Assert.AreEqual(DependencyScope.MavenCompile, componentAndMetaData.dependencyScope);
        }

        [TestMethod]
        public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_DevelopmentDependencyTrue()
        {
            var componentAndMetaData =
                GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT:test");

            Assert.IsNotNull(componentAndMetaData);
            Assert.IsNotNull(componentAndMetaData.IsDevelopmentDependency);

            var actualComponent = (MavenComponent)componentAndMetaData.Component.Component;
            Assert.IsInstanceOfType(actualComponent, typeof(MavenComponent));
            Assert.IsTrue(componentAndMetaData.IsDevelopmentDependency);
        }

        [TestMethod]
        public void GenerateDetectedComponentAndIsDeveDependencyAndDependencyScope_InvalidScope()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-artifact:jar:3.6.1-SNAPSHOT:invalidScope"));
            Assert.IsTrue(ex.Message.Contains("invalid scope", StringComparison.OrdinalIgnoreCase));
        }
    }
}

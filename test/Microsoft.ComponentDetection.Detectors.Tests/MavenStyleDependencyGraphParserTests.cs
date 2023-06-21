namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MavenStyleDependencyGraphParserTests
{
    private readonly string sampleMavenDependencyTreePath = Path.Combine("Mocks", "MvnCliDependencyOutput.txt");

    [TestMethod]
    public void MavenFormat_ExpectedParse()
    {
        var sampleMavenDependencyTree = File.ReadAllLines(this.sampleMavenDependencyTreePath);

        var parser = new MavenStyleDependencyGraphParser();
        var parsedGraph = parser.Parse(sampleMavenDependencyTree);
        Assert.AreEqual(parsedGraph.Children.Count, 20);
        Assert.AreEqual(parsedGraph.Value, "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT");

        // Verify a specific interesting path:
        var mavenCore = parsedGraph.Children.FirstOrDefault(x => x.Value == "org.apache.maven:maven-core:jar:3.6.1-SNAPSHOT:compile");
        Assert.IsNotNull(mavenCore);
        Assert.AreEqual(mavenCore.Children.Count, 7);

        var guice = mavenCore.Children.FirstOrDefault(x => x.Value == "com.google.inject:guice:jar:no_aop:4.2.1:compile");
        Assert.IsNotNull(guice);
        Assert.AreEqual(guice.Children.Count, 2);

        var guava = guice.Children.FirstOrDefault(x => x.Value == "com.google.guava:guava:jar:25.1-android:compile");
        Assert.IsNotNull(guava);
        Assert.AreEqual(guava.Children.Count, 5);

        var animalSnifferAnnotations = guava.Children.FirstOrDefault(x => x.Value == "org.codehaus.mojo:animal-sniffer-annotations:jar:1.14:compile");
        Assert.IsNotNull(animalSnifferAnnotations);
        Assert.AreEqual(animalSnifferAnnotations.Children.Count, 0);
    }

    [TestMethod]
    public void MavenFormat_WithSingleFileComponentRecorder_ExpectedParse()
    {
        var sampleMavenDependencyTree = File.ReadAllLines(this.sampleMavenDependencyTreePath);

        var parser = new MavenStyleDependencyGraphParser();

        var componentRecorder = new ComponentRecorder();
        var pomfileLocation = "location";

        parser.Parse(sampleMavenDependencyTree, componentRecorder.CreateSingleFileComponentRecorder(pomfileLocation));

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation()[pomfileLocation];

        var (component, isDevelopmentDependency, dependencyScope) = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT");
        var topLevelComponent = component;
        var mavenCore = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString("org.apache.maven:maven-core:jar:3.6.1-SNAPSHOT:compile").Component;
        var guice = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString("com.google.inject:guice:jar:no_aop:4.2.1:compile").Component;
        var guava = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString("com.google.guava:guava:jar:25.1-android:compile").Component;
        var animalSnifferAnnotations = MavenParsingUtilities.GenerateDetectedComponentAndMetadataFromMavenString("org.codehaus.mojo:animal-sniffer-annotations:jar:1.14:compile").Component;

        var topLevelComponentDependencies = dependencyGraph.GetDependenciesForComponent(topLevelComponent.Component.Id);
        topLevelComponentDependencies.Should().HaveCount(20);
        topLevelComponentDependencies.Should().Contain(mavenCore.Component.Id);
        topLevelComponentDependencies.All(componentId => dependencyGraph.IsComponentExplicitlyReferenced(componentId)).Should().BeTrue();

        var mavenCoreDependencies = dependencyGraph.GetDependenciesForComponent(mavenCore.Component.Id);
        mavenCoreDependencies.Should().HaveCount(7);
        mavenCoreDependencies.Should().Contain(guice.Component.Id);

        var guiceDependencies = dependencyGraph.GetDependenciesForComponent(guice.Component.Id);
        guiceDependencies.Should().HaveCount(2);
        guiceDependencies.Should().Contain(guava.Component.Id);

        var guavaDependencies = dependencyGraph.GetDependenciesForComponent(guava.Component.Id);
        guavaDependencies.Should().HaveCount(5);
        guavaDependencies.Should().Contain(animalSnifferAnnotations.Component.Id);

        dependencyGraph.GetDependenciesForComponent(animalSnifferAnnotations.Component.Id).Should().HaveCount(0);
    }
}

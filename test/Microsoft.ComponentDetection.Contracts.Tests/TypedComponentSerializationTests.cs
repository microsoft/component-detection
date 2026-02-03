#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class TypedComponentSerializationTests
{
    [TestMethod]
    public void TypedComponent_Serialization_Other()
    {
        TypedComponent tc = new OtherComponent("SomeOtherComponent", "1.2.3", new Uri("https://sampleurl.com"), "SampleHash");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(OtherComponent));
        var otherComponent = (OtherComponent)deserializedTC;
        otherComponent.Name.Should().Be("SomeOtherComponent");
        otherComponent.Version.Should().Be("1.2.3");
        otherComponent.DownloadUrl.Should().Be(new Uri("https://sampleurl.com"));
        otherComponent.Hash.Should().Be("SampleHash");
    }

    [TestMethod]
    public void TypedComponent_Serialization_NuGet()
    {
        var testComponentName = "SomeNuGetComponent";
        var testVersion = "1.2.3";
        string[] testAuthors = ["John Doe", "Jane Doe"];
        TypedComponent tc = new NuGetComponent(testComponentName, testVersion, testAuthors);
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(NuGetComponent));
        var nugetComponent = (NuGetComponent)deserializedTC;
        nugetComponent.Name.Should().Be(testComponentName);
        nugetComponent.Version.Should().Be(testVersion);
        nugetComponent.Authors.Should().BeEquivalentTo(testAuthors);
    }

    [TestMethod]
    public void TypedComponent_Serialization_Npm()
    {
        var npmAuthor = new NpmAuthor("someAuthorName", "someAuthorEmail");
        TypedComponent npmCompObj = new NpmComponent("SomeNpmComponent", "1.2.3")
        {
            Author = npmAuthor,
        };
        var result = JsonSerializer.Serialize(npmCompObj);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(NpmComponent));
        var npmComponent = (NpmComponent)deserializedTC;
        npmComponent.Name.Should().Be("SomeNpmComponent");
        npmComponent.Version.Should().Be("1.2.3");
        npmComponent.Author.Should().BeEquivalentTo(npmAuthor);
    }

    [TestMethod]
    public void TypedComponent_Serialization_Npm_WithHash()
    {
        TypedComponent tc = new NpmComponent("SomeNpmComponent", "1.2.3", "sha1-placeholder");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(NpmComponent));
        var npmComponent = (NpmComponent)deserializedTC;
        npmComponent.Name.Should().Be("SomeNpmComponent");
        npmComponent.Version.Should().Be("1.2.3");
        npmComponent.Hash.Should().Be("sha1-placeholder");
    }

    [TestMethod]
    public void TypedComponent_Serialization_Maven()
    {
        TypedComponent tc = new MavenComponent("SomeGroupId", "SomeArtifactId", "1.2.3");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(MavenComponent));
        var mavenComponent = (MavenComponent)deserializedTC;
        mavenComponent.GroupId.Should().Be("SomeGroupId");
        mavenComponent.ArtifactId.Should().Be("SomeArtifactId");
        mavenComponent.Version.Should().Be("1.2.3");
    }

    [TestMethod]
    public void TypedComponent_Serialization_Git()
    {
        TypedComponent tc = new GitComponent(new Uri("http://some.com/git/url.git"), "SomeHash");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(GitComponent));
        var gitComponent = (GitComponent)deserializedTC;
        gitComponent.RepositoryUrl.Should().Be(new Uri("http://some.com/git/url.git"));
        gitComponent.CommitHash.Should().Be("SomeHash");
    }

    [TestMethod]
    public void TypedComponent_Serialization_RubyGems()
    {
        TypedComponent tc = new RubyGemsComponent("SomeGem", "1.2.3", "SampleSource");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(RubyGemsComponent));
        var rubyGemComponent = (RubyGemsComponent)deserializedTC;
        rubyGemComponent.Name.Should().Be("SomeGem");
        rubyGemComponent.Version.Should().Be("1.2.3");
        rubyGemComponent.Source.Should().Be("SampleSource");
    }

    [TestMethod]
    public void TypedComponent_Serialization_Cargo()
    {
        TypedComponent tc = new CargoComponent("SomeCargoPackage", "1.2.3");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(CargoComponent));
        var cargoComponent = (CargoComponent)deserializedTC;
        cargoComponent.Name.Should().Be("SomeCargoPackage");
        cargoComponent.Version.Should().Be("1.2.3");
    }

    [TestMethod]
    public void TypedComponent_Serialization_Conan()
    {
        var md5 = Guid.NewGuid().ToString();
        var sha1Hash = Guid.NewGuid().ToString();
        TypedComponent tc = new ConanComponent("SomeConanPackage", "1.2.3", md5, sha1Hash);
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(ConanComponent));
        var conanComponent = (ConanComponent)deserializedTC;
        conanComponent.Name.Should().Be("SomeConanPackage");
        conanComponent.Version.Should().Be("1.2.3");
        conanComponent.Md5Hash.Should().Be(md5);
        conanComponent.Sha1Hash.Should().Be(sha1Hash);
        conanComponent.PackageSourceURL.Should().Be("https://conan.io/center/recipes/SomeConanPackage?version=1.2.3");
    }

    [TestMethod]
    public void TypedComponent_Serialization_Pip()
    {
        TypedComponent tc = new PipComponent("SomePipPackage", "1.2.3");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(PipComponent));
        var pipComponent = (PipComponent)deserializedTC;
        pipComponent.Name.Should().Be("SomePipPackage");
        pipComponent.Version.Should().Be("1.2.3");
    }

    [TestMethod]
    public void TypedComponent_Serialization_Go()
    {
        TypedComponent tc = new GoComponent("SomeGoPackage", "1.2.3", "SomeHash");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(GoComponent));
        var goComponent = (GoComponent)deserializedTC;
        goComponent.Name.Should().Be("SomeGoPackage");
        goComponent.Version.Should().Be("1.2.3");
        goComponent.Hash.Should().Be("SomeHash");
    }

    [TestMethod]
    public void TypedComponent_Serialization_DockerImage()
    {
        TypedComponent tc = new DockerImageComponent("SomeImageHash", "SomeImageName", "SomeImageTag");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(DockerImageComponent));
        var dockerImageComponent = (DockerImageComponent)deserializedTC;
        dockerImageComponent.Digest.Should().Be("SomeImageHash");
        dockerImageComponent.Name.Should().Be("SomeImageName");
        dockerImageComponent.Tag.Should().Be("SomeImageTag");
    }

    [TestMethod]
    public void TypedComponent_Serialization_PodComponent()
    {
        TypedComponent tc = new PodComponent("SomePodName", "SomePodVersion", "SomeSpecRepo");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(PodComponent));
        var podComponent = (PodComponent)deserializedTC;
        podComponent.Name.Should().Be("SomePodName");
        podComponent.Version.Should().Be("SomePodVersion");
        podComponent.SpecRepo.Should().Be("SomeSpecRepo");
    }

    [TestMethod]
    public void TypedComponent_Serialization_LinuxComponent()
    {
        TypedComponent tc = new LinuxComponent("SomeLinuxDistribution", "SomeLinuxRelease", "SomeLinuxComponentName", "SomeLinuxComponentVersion");
        var result = JsonSerializer.Serialize(tc);
        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(result);
        deserializedTC.Should().BeOfType(typeof(LinuxComponent));
        var linuxComponent = (LinuxComponent)deserializedTC;
        linuxComponent.Distribution.Should().Be("SomeLinuxDistribution");
        linuxComponent.Release.Should().Be("SomeLinuxRelease");
        linuxComponent.Name.Should().Be("SomeLinuxComponentName");
        linuxComponent.Version.Should().Be("SomeLinuxComponentVersion");
    }

    [TestMethod]
    public void TypedComponent_Deserialization_UnknownComponentType_ReturnsNull()
    {
        // Simulate a JSON payload with a component type that doesn't exist in the current version
        // This tests forward compatibility when new component types are added
        var unknownComponentJson = """
            {
                "type": "FutureComponentType",
                "name": "SomeComponent",
                "version": "1.0.0"
            }
            """;

        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(unknownComponentJson);
        deserializedTC.Should().BeNull();
    }

    [TestMethod]
    public void TypedComponent_Deserialization_InvalidComponentType_ReturnsNull()
    {
        // Test with a completely invalid/malformed type value
        var invalidComponentJson = """
            {
                "type": "Not_A_Valid_Enum_Value_12345",
                "name": "SomeComponent",
                "version": "1.0.0"
            }
            """;

        var deserializedTC = JsonSerializer.Deserialize<TypedComponent>(invalidComponentJson);
        deserializedTC.Should().BeNull();
    }

    [TestMethod]
    public void TypedComponentMapping_AllComponentTypes_HaveMapping()
    {
        // Ensure every ComponentType enum value has a corresponding entry in the mapping
        // This prevents forgetting to add new component types to the serialization mapping
        var allComponentTypes = Enum.GetValues(typeof(ComponentType)).Cast<ComponentType>();
        var mappedTypes = TypedComponentMapping.TypeDiscriminatorToType;

        foreach (var componentType in allComponentTypes)
        {
            var typeName = componentType.ToString();
            mappedTypes.Should().ContainKey(typeName, $"ComponentType.{typeName} should have a mapping in TypedComponentMapping");
        }
    }

    [TestMethod]
    public void TypedComponentMapping_AllMappedTypes_AreTypedComponentSubclasses()
    {
        // Ensure all mapped types are actually subclasses of TypedComponent
        foreach (var kvp in TypedComponentMapping.TypeDiscriminatorToType)
        {
            kvp.Value.Should().BeAssignableTo<TypedComponent>($"Mapped type for '{kvp.Key}' should be a TypedComponent subclass");
        }
    }

    [TestMethod]
    public void TypedComponentMapping_AllMappedTypes_AreUnique()
    {
        // Ensure no two discriminators map to the same type
        var mappedTypes = TypedComponentMapping.TypeDiscriminatorToType.Values.ToList();
        var uniqueTypes = mappedTypes.Distinct().ToList();

        mappedTypes.Should().HaveCount(uniqueTypes.Count, "Each component type should map to a unique concrete type");
    }

    [TestMethod]
    public void TypedComponent_Serialization_TypePropertyNotDuplicated()
    {
        // Ensure the "type" property is only serialized once at the root level, not duplicated
        TypedComponent tc = new NpmComponent("TestPackage", "1.0.0");
        var json = JsonSerializer.Serialize(tc);

        // Parse the JSON and check root-level properties only
        using var doc = JsonDocument.Parse(json);
        var typeProperties = doc.RootElement.EnumerateObject()
            .Count(p => p.Name.Equals("type", StringComparison.OrdinalIgnoreCase));

        typeProperties.Should().Be(1, "The 'type' property should appear exactly once at the root level in the serialized JSON");
    }

    [TestMethod]
    public void TypedComponent_Serialization_AllComponentTypes_TypePropertyNotDuplicated()
    {
        // Test all component types to ensure none have duplicate "type" properties at the root level
        var testComponents = new TypedComponent[]
        {
            new NpmComponent("test", "1.0.0"),
            new NuGetComponent("test", "1.0.0"),
            new MavenComponent("group", "artifact", "1.0.0"),
            new PipComponent("test", "1.0.0"),
            new GoComponent("test", "1.0.0"),
            new CargoComponent("test", "1.0.0"),
            new RubyGemsComponent("test", "1.0.0"),
            new GitComponent(new Uri("https://github.com/test/test"), "abc123"),
            new OtherComponent("test", "1.0.0", new Uri("https://example.com"), "hash"),
        };

        foreach (var component in testComponents)
        {
            var json = JsonSerializer.Serialize(component);

            // Parse the JSON and check root-level properties only
            using var doc = JsonDocument.Parse(json);
            var typeProperties = doc.RootElement.EnumerateObject()
                .Count(p => p.Name.Equals("type", StringComparison.OrdinalIgnoreCase));

            typeProperties.Should().Be(1, $"The 'type' property should appear exactly once at the root level for {component.Type}");
        }
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Microsoft.ComponentDetection.Detectors.Linux.Factories;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ArtifactComponentFactoryTests
{
    private static readonly Distro TestDistro = new() { Id = "ubuntu", VersionId = "22.04" };

    [TestMethod]
    public void RubyGemsComponentFactory_CreatesComponent_WithNameAndVersion()
    {
        var factory = new RubyGemsComponentFactory();
        var artifact = new ArtifactElement { Name = "rails", Version = "7.0.4" };

        var result = factory.CreateComponent(artifact, TestDistro) as RubyGemsComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("rails");
        result.Version.Should().Be("7.0.4");
    }

    [TestMethod]
    public void RubyGemsComponentFactory_ReturnsNull_WhenArtifactIsNull()
    {
        var factory = new RubyGemsComponentFactory();

        var result = factory.CreateComponent(null, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void RubyGemsComponentFactory_ReturnsNull_WhenNameIsEmpty()
    {
        var factory = new RubyGemsComponentFactory();
        var artifact = new ArtifactElement { Name = string.Empty, Version = "7.0.4" };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void RubyGemsComponentFactory_ReturnsNull_WhenVersionIsEmpty()
    {
        var factory = new RubyGemsComponentFactory();
        var artifact = new ArtifactElement { Name = "rails", Version = string.Empty };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void RubyGemsComponentFactory_SupportedArtifactTypes_ContainsGem()
    {
        var factory = new RubyGemsComponentFactory();

        factory.SupportedArtifactTypes.Should().Contain("gem");
    }

    [TestMethod]
    public void GoComponentFactory_CreatesComponent_WithNameAndVersion()
    {
        var factory = new GoComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "github.com/gin-gonic/gin",
            Version = "v1.9.1",
        };

        var result = factory.CreateComponent(artifact, TestDistro) as GoComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("github.com/gin-gonic/gin");
        result.Version.Should().Be("v1.9.1");
        result.Hash.Should().BeEmpty();
    }

    [TestMethod]
    public void GoComponentFactory_CreatesComponent_WithHash()
    {
        var factory = new GoComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "github.com/gin-gonic/gin",
            Version = "v1.9.1",
            Metadata = new MetadataClass { H1Digest = "h1:abc123=" },
        };

        var result = factory.CreateComponent(artifact, TestDistro) as GoComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("github.com/gin-gonic/gin");
        result.Version.Should().Be("v1.9.1");
        result.Hash.Should().Be("h1:abc123=");
    }

    [TestMethod]
    public void GoComponentFactory_ReturnsNull_WhenArtifactIsNull()
    {
        var factory = new GoComponentFactory();

        var result = factory.CreateComponent(null, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GoComponentFactory_ReturnsNull_WhenNameIsEmpty()
    {
        var factory = new GoComponentFactory();
        var artifact = new ArtifactElement { Name = string.Empty, Version = "v1.9.1" };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GoComponentFactory_SupportedArtifactTypes_ContainsGoModule()
    {
        var factory = new GoComponentFactory();

        factory.SupportedArtifactTypes.Should().Contain("go-module");
    }

    [TestMethod]
    public void CargoComponentFactory_CreatesComponent_WithNameAndVersion()
    {
        var factory = new CargoComponentFactory();
        var artifact = new ArtifactElement { Name = "serde", Version = "1.0.188" };

        var result = factory.CreateComponent(artifact, TestDistro) as CargoComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("serde");
        result.Version.Should().Be("1.0.188");
    }

    [TestMethod]
    public void CargoComponentFactory_CreatesComponent_WithOptionalFields()
    {
        var factory = new CargoComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "serde",
            Version = "1.0.188",
            Metadata = new MetadataClass { Author = "Erick Tryzelaar" },
            Licenses = [new ArtifactLicense { Value = "MIT OR Apache-2.0" }],
        };

        var result = factory.CreateComponent(artifact, TestDistro) as CargoComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("serde");
        result.Version.Should().Be("1.0.188");
        result.Author.Should().Be("Erick Tryzelaar");
        result.License.Should().Be("MIT OR Apache-2.0");
    }

    [TestMethod]
    public void CargoComponentFactory_ReturnsNull_WhenArtifactIsNull()
    {
        var factory = new CargoComponentFactory();

        var result = factory.CreateComponent(null, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void CargoComponentFactory_ReturnsNull_WhenVersionIsEmpty()
    {
        var factory = new CargoComponentFactory();
        var artifact = new ArtifactElement { Name = "serde", Version = string.Empty };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void CargoComponentFactory_SupportedArtifactTypes_ContainsRustCrate()
    {
        var factory = new CargoComponentFactory();

        factory.SupportedArtifactTypes.Should().Contain("rust-crate");
    }

    [TestMethod]
    public void PodComponentFactory_CreatesComponent_WithNameAndVersion()
    {
        var factory = new PodComponentFactory();
        var artifact = new ArtifactElement { Name = "Alamofire", Version = "5.8.0" };

        var result = factory.CreateComponent(artifact, TestDistro) as PodComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("Alamofire");
        result.Version.Should().Be("5.8.0");
    }

    [TestMethod]
    public void PodComponentFactory_ReturnsNull_WhenArtifactIsNull()
    {
        var factory = new PodComponentFactory();

        var result = factory.CreateComponent(null, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void PodComponentFactory_ReturnsNull_WhenNameIsEmpty()
    {
        var factory = new PodComponentFactory();
        var artifact = new ArtifactElement { Name = string.Empty, Version = "5.8.0" };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void PodComponentFactory_SupportedArtifactTypes_ContainsPod()
    {
        var factory = new PodComponentFactory();

        factory.SupportedArtifactTypes.Should().Contain("pod");
    }

    [TestMethod]
    public void CondaComponentFactory_CreatesComponent_WithNameAndVersion()
    {
        var factory = new CondaComponentFactory();
        var artifact = new ArtifactElement { Name = "numpy", Version = "1.24.3" };

        var result = factory.CreateComponent(artifact, TestDistro) as CondaComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("numpy");
        result.Version.Should().Be("1.24.3");
    }

    [TestMethod]
    public void CondaComponentFactory_CreatesComponent_WithFullMetadata()
    {
        var factory = new CondaComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "numpy",
            Version = "1.24.3",
            Metadata = new MetadataClass
            {
                Build = "py311h53ef19d_0",
                Channel = "https://conda.anaconda.org/conda-forge",
                Subdir = "linux-64",
                Md5 = "abc123def456",
            },
        };

        var result = factory.CreateComponent(artifact, TestDistro) as CondaComponent;

        result.Should().NotBeNull();
        result.Name.Should().Be("numpy");
        result.Version.Should().Be("1.24.3");
        result.Build.Should().Be("py311h53ef19d_0");
        result.Channel.Should().Be("https://conda.anaconda.org/conda-forge");
        result.Subdir.Should().Be("linux-64");
        result.MD5.Should().Be("abc123def456");
    }

    [TestMethod]
    public void CondaComponentFactory_ReturnsNull_WhenArtifactIsNull()
    {
        var factory = new CondaComponentFactory();

        var result = factory.CreateComponent(null, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void CondaComponentFactory_ReturnsNull_WhenVersionIsEmpty()
    {
        var factory = new CondaComponentFactory();
        var artifact = new ArtifactElement { Name = "numpy", Version = string.Empty };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void CondaComponentFactory_SupportedArtifactTypes_ContainsConda()
    {
        var factory = new CondaComponentFactory();

        factory.SupportedArtifactTypes.Should().Contain("conda");
    }

    [TestMethod]
    public void MavenComponentFactory_CreatesComponent_FromPomProperties()
    {
        var factory = new MavenComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "spring-boot",
            Version = "3.1.5",
            Metadata = new MetadataClass
            {
                PomProperties = new PomProperties
                {
                    GroupId = "org.springframework.boot",
                    ArtifactId = "spring-boot",
                },
            },
        };

        var result = factory.CreateComponent(artifact, TestDistro) as MavenComponent;

        result.Should().NotBeNull();
        result.GroupId.Should().Be("org.springframework.boot");
        result.ArtifactId.Should().Be("spring-boot");
        result.Version.Should().Be("3.1.5");
    }

    [TestMethod]
    public void MavenComponentFactory_CreatesComponent_FromPomProject()
    {
        var factory = new MavenComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "spring-boot",
            Version = "3.1.5",
            Metadata = new MetadataClass
            {
                PomProject = new PomProject
                {
                    GroupId = "org.springframework.boot",
                    ArtifactId = "spring-boot",
                },
            },
        };

        var result = factory.CreateComponent(artifact, TestDistro) as MavenComponent;

        result.Should().NotBeNull();
        result.GroupId.Should().Be("org.springframework.boot");
        result.ArtifactId.Should().Be("spring-boot");
        result.Version.Should().Be("3.1.5");
    }

    [TestMethod]
    public void MavenComponentFactory_CreatesComponent_FromColonSeparatedName()
    {
        var factory = new MavenComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "org.springframework.boot:spring-boot",
            Version = "3.1.5",
        };

        var result = factory.CreateComponent(artifact, TestDistro) as MavenComponent;

        result.Should().NotBeNull();
        result.GroupId.Should().Be("org.springframework.boot");
        result.ArtifactId.Should().Be("spring-boot");
        result.Version.Should().Be("3.1.5");
    }

    [TestMethod]
    public void MavenComponentFactory_PrefersPomProperties_OverPomProject()
    {
        var factory = new MavenComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "spring-boot",
            Version = "3.1.5",
            Metadata = new MetadataClass
            {
                PomProperties = new PomProperties
                {
                    GroupId = "org.springframework.boot",
                    ArtifactId = "spring-boot-properties",
                },
                PomProject = new PomProject
                {
                    GroupId = "org.springframework.boot",
                    ArtifactId = "spring-boot-project",
                },
            },
        };

        var result = factory.CreateComponent(artifact, TestDistro) as MavenComponent;

        result.Should().NotBeNull();
        result.ArtifactId.Should().Be("spring-boot-properties");
    }

    [TestMethod]
    public void MavenComponentFactory_ReturnsNull_WhenCannotDetermineMavenCoordinates()
    {
        var factory = new MavenComponentFactory();
        var artifact = new ArtifactElement { Name = "just-a-jar-name", Version = "1.0.0" };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void MavenComponentFactory_ReturnsNull_WhenArtifactIsNull()
    {
        var factory = new MavenComponentFactory();

        var result = factory.CreateComponent(null, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void MavenComponentFactory_ReturnsNull_WhenVersionIsEmpty()
    {
        var factory = new MavenComponentFactory();
        var artifact = new ArtifactElement
        {
            Name = "org.springframework.boot:spring-boot",
            Version = string.Empty,
        };

        var result = factory.CreateComponent(artifact, TestDistro);

        result.Should().BeNull();
    }

    [TestMethod]
    public void MavenComponentFactory_SupportedArtifactTypes_ContainsJavaArchive()
    {
        var factory = new MavenComponentFactory();

        factory.SupportedArtifactTypes.Should().Contain("java-archive");
    }
}

using System;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Contracts.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class TypedComponentSerializationTests
    {
        [TestMethod]
        public void TypedComponent_Serialization_Other()
        {
            TypedComponent.TypedComponent tc = new OtherComponent("SomeOtherComponent", "1.2.3", new Uri("https://sampleurl.com"), "SampleHash");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
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
            string[] testAuthors = { "John Doe", "Jane Doe" };
            TypedComponent.TypedComponent tc = new NuGetComponent(testComponentName, testVersion, testAuthors);
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
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
            var npmCompObj = new NpmComponent("SomeNpmComponent", "1.2.3")
            {
                Author = npmAuthor,
            };
            var result = JsonConvert.SerializeObject(npmCompObj);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(NpmComponent));
            var npmComponent = (NpmComponent)deserializedTC;
            npmComponent.Name.Should().Be("SomeNpmComponent");
            npmComponent.Version.Should().Be("1.2.3");
            npmComponent.Author.Should().BeEquivalentTo(npmAuthor);
        }

        [TestMethod]
        public void TypedComponent_Serialization_Npm_WithHash()
        {
            TypedComponent.TypedComponent tc = new NpmComponent("SomeNpmComponent", "1.2.3", "sha1-placeholder");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(NpmComponent));
            var npmComponent = (NpmComponent)deserializedTC;
            npmComponent.Name.Should().Be("SomeNpmComponent");
            npmComponent.Version.Should().Be("1.2.3");
            npmComponent.Hash.Should().Be("sha1-placeholder");
        }

        [TestMethod]
        public void TypedComponent_Serialization_Maven()
        {
            TypedComponent.TypedComponent tc = new MavenComponent("SomeGroupId", "SomeArtifactId", "1.2.3");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(MavenComponent));
            var mavenComponent = (MavenComponent)deserializedTC;
            mavenComponent.GroupId.Should().Be("SomeGroupId");
            mavenComponent.ArtifactId.Should().Be("SomeArtifactId");
            mavenComponent.Version.Should().Be("1.2.3");
        }

        [TestMethod]
        public void TypedComponent_Serialization_Git()
        {
            TypedComponent.TypedComponent tc = new GitComponent(new Uri("http://some.com/git/url.git"), "SomeHash");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(GitComponent));
            var gitComponent = (GitComponent)deserializedTC;
            gitComponent.RepositoryUrl.Should().Be(new Uri("http://some.com/git/url.git"));
            gitComponent.CommitHash.Should().Be("SomeHash");
        }

        [TestMethod]
        public void TypedComponent_Serialization_RubyGems()
        {
            TypedComponent.TypedComponent tc = new RubyGemsComponent("SomeGem", "1.2.3", "SampleSource");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(RubyGemsComponent));
            var rubyGemComponent = (RubyGemsComponent)deserializedTC;
            rubyGemComponent.Name.Should().Be("SomeGem");
            rubyGemComponent.Version.Should().Be("1.2.3");
            rubyGemComponent.Source.Should().Be("SampleSource");
        }

        [TestMethod]
        public void TypedComponent_Serialization_Cargo()
        {
            TypedComponent.TypedComponent tc = new CargoComponent("SomeCargoPackage", "1.2.3");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(CargoComponent));
            var cargoComponent = (CargoComponent)deserializedTC;
            cargoComponent.Name.Should().Be("SomeCargoPackage");
            cargoComponent.Version.Should().Be("1.2.3");
        }

        [TestMethod]
        public void TypedComponent_Serialization_Pip()
        {
            TypedComponent.TypedComponent tc = new PipComponent("SomePipPackage", "1.2.3");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(PipComponent));
            var pipComponent = (PipComponent)deserializedTC;
            pipComponent.Name.Should().Be("SomePipPackage");
            pipComponent.Version.Should().Be("1.2.3");
        }

        [TestMethod]
        public void TypedComponent_Serialization_Go()
        {
            TypedComponent.TypedComponent tc = new GoComponent("SomeGoPackage", "1.2.3", "SomeHash");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(GoComponent));
            var goComponent = (GoComponent)deserializedTC;
            goComponent.Name.Should().Be("SomeGoPackage");
            goComponent.Version.Should().Be("1.2.3");
            goComponent.Hash.Should().Be("SomeHash");
        }

        [TestMethod]
        public void TypedComponent_Serialization_DockerImage()
        {
            TypedComponent.TypedComponent tc = new DockerImageComponent("SomeImageHash", "SomeImageName", "SomeImageTag");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(DockerImageComponent));
            var dockerImageComponent = (DockerImageComponent)deserializedTC;
            dockerImageComponent.Digest.Should().Be("SomeImageHash");
            dockerImageComponent.Name.Should().Be("SomeImageName");
            dockerImageComponent.Tag.Should().Be("SomeImageTag");
        }

        [TestMethod]
        public void TypedComponent_Serialization_PodComponent()
        {
            TypedComponent.TypedComponent tc = new PodComponent("SomePodName", "SomePodVersion", "SomeSpecRepo");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(PodComponent));
            var podComponent = (PodComponent)deserializedTC;
            podComponent.Name.Should().Be("SomePodName");
            podComponent.Version.Should().Be("SomePodVersion");
            podComponent.SpecRepo.Should().Be("SomeSpecRepo");
        }

        [TestMethod]
        public void TypedComponent_Serialization_LinuxComponent()
        {
            TypedComponent.TypedComponent tc = new LinuxComponent("SomeLinuxDistribution", "SomeLinuxRelease", "SomeLinuxComponentName", "SomeLinuxComponentVersion");
            var result = JsonConvert.SerializeObject(tc);
            var deserializedTC = JsonConvert.DeserializeObject<TypedComponent.TypedComponent>(result);
            deserializedTC.Should().BeOfType(typeof(LinuxComponent));
            var linuxComponent = (LinuxComponent)deserializedTC;
            linuxComponent.Distribution.Should().Be("SomeLinuxDistribution");
            linuxComponent.Release.Should().Be("SomeLinuxRelease");
            linuxComponent.Name.Should().Be("SomeLinuxComponentName");
            linuxComponent.Version.Should().Be("SomeLinuxComponentVersion");
        }
    }
}

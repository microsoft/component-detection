#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DockerReferenceUtilityTests
{
    [TestMethod]
    public void ParseQualifiedName_ThrowsReferenceNameEmptyException()
    {
        var qualifiedName = string.Empty;

        var func = () => DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        func.Should().Throw<ReferenceNameEmptyException>();
    }

    [TestMethod]
    public void ParseQualifiedName_ThrowsReferenceNameContainsUppercaseException()
    {
        var qualifiedName = "docker.io/library/Nginx";

        var func = () => DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        func.Should().Throw<ReferenceNameContainsUppercaseException>();
    }

    [TestMethod]
    public void ParseQualifiedName_ThrowsReferenceInvalidFormatException()
    {
        var qualifiedName = "docker.io/library/nginx:latest:latest";

        var func = () => DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        func.Should().Throw<ReferenceInvalidFormatException>();
    }

    [TestMethod]
    public void ParseQualifiedName_ThrowsReferenceNameTooLongException()
    {
        var qualifiedName = $"docker.io/library/{"nginx".PadRight(256, 'a')}";

        var func = () => DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        func.Should().Throw<ReferenceNameTooLongException>();
    }

    [TestMethod]
    public void ParseQualifiedName_CreatesProperTaggedReference()
    {
        var qualifiedName = "docker.io/library/nginx:latest";

        var result = DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<TaggedReference>();

        var taggedReference = (TaggedReference)result;
        taggedReference.Should().NotBeNull();
        taggedReference.Domain.Should().Be("docker.io");
        taggedReference.Repository.Should().Be("library/nginx");
        taggedReference.Tag.Should().Be("latest");
    }

    [TestMethod]
    public void ParseQualifiedName_CreatesProperRepositoryReference()
    {
        var qualifiedName = "docker.io/library/nginx";

        var result = DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<RepositoryReference>();

        var repositoryReference = (RepositoryReference)result;
        repositoryReference.Should().NotBeNull();
        repositoryReference.Domain.Should().Be("docker.io");
        repositoryReference.Repository.Should().Be("library/nginx");
    }

    [TestMethod]
    public void ParseQualifiedName_ThrowsInvalidOperationIfNoComponent()
    {
        var qualifiedName = "nginx";

        var func = () => DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        func.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void ParseQualifiedName_CreatesProperCanonicalReference()
    {
        var hashTag = $"sha256:{new string('a', 64)}";
        var qualifiedName = $"docker.io/library/nginx@{hashTag}";

        var result = DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<CanonicalReference>();

        var digestReference = (CanonicalReference)result;
        digestReference.Should().NotBeNull();
        digestReference.Domain.Should().Be("docker.io");
        digestReference.Repository.Should().Be("library/nginx");
        digestReference.Digest.Should().Be(hashTag);
    }

    [TestMethod]
    public void ParseQualifiedName_CreatedProperDigestReference()
    {
        var hashTag = $"sha256:{new string('a', 64)}";
        var qualifiedName = $"nginx@{hashTag}";

        var result = DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<DigestReference>();

        var digestReference = (DigestReference)result;
        digestReference.Digest.Should().Be(hashTag);
    }

    [TestMethod]
    public void ParseQualifiedName_CreatedDualReference()
    {
        var hashTag = $"sha256:{new string('a', 64)}";
        var qualifiedName = $"docker.io/library/nginx:latest@{hashTag}";

        var result = DockerReferenceUtility.ParseQualifiedName(qualifiedName);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<DualReference>();

        var dualReference = (DualReference)result;
        dualReference.Should().NotBeNull();
        dualReference.Domain.Should().Be("docker.io");
        dualReference.Repository.Should().Be("library/nginx");
        dualReference.Digest.Should().Be(hashTag);
        dualReference.Tag.Should().Be("latest");
    }

    [TestMethod]
    public void SplitDockerDomain_AddsDefaultDomain()
    {
        var name = "library/nginx";

        var result = DockerReferenceUtility.SplitDockerDomain(name);

        result.Should().NotBeNull();
        result.Domain.Should().Be("docker.io");
        result.Remainder.Should().Be("library/nginx");
    }

    [TestMethod]
    public void SplitDockerDomain_ReplacesLegacyDefaultDomain()
    {
        var name = "index.docker.io/library/nginx";

        var result = DockerReferenceUtility.SplitDockerDomain(name);

        result.Should().NotBeNull();
        result.Domain.Should().Be("docker.io");
        result.Remainder.Should().Be("library/nginx");
    }

    [TestMethod]
    public void SplitDockerDomain_UpdatesRemainderWithOfficialRepoName()
    {
        var name = "nginx";

        var result = DockerReferenceUtility.SplitDockerDomain(name);

        result.Should().NotBeNull();
        result.Domain.Should().Be("docker.io");
        result.Remainder.Should().Be("library/nginx");
    }

    [TestMethod]
    public void SplitDockerDomain_Works()
    {
        var name = "docker.io/library/nginx";

        var result = DockerReferenceUtility.SplitDockerDomain(name);

        result.Should().NotBeNull();
        result.Domain.Should().Be("docker.io");
        result.Remainder.Should().Be("library/nginx");
    }

    [TestMethod]
    public void ParseFamiliarName_ThrowsReferenceNameNotCanonicalException()
    {
        var name = new string('a', 64);

        var func = () => DockerReferenceUtility.ParseFamiliarName(name);

        func.Should().Throw<ReferenceNameNotCanonicalException>();
    }

    [TestMethod]
    public void ParseFamiliarName_HandlesMissingTagSeperator()
    {
        var name = "docker.io/library/nginx";

        var result = DockerReferenceUtility.ParseFamiliarName(name);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<RepositoryReference>();
    }

    [TestMethod]
    public void ParseFamiliarName_HandlesTag()
    {
        var name = "docker.io/library/nginx:latest";

        var result = DockerReferenceUtility.ParseFamiliarName(name);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<TaggedReference>();
    }

    [TestMethod]
    public void ParseFamiliarName_ThrowsReferenceNameContainsUppercaseException()
    {
        var name = "docker.io/library/Nginx";

        var func = () => DockerReferenceUtility.ParseFamiliarName(name);

        func.Should().Throw<ReferenceNameContainsUppercaseException>();
    }

    [TestMethod]
    public void ParseAll_HandlesAnchoredIdentifiers()
    {
        var name = new string('a', 64);

        var result = DockerReferenceUtility.ParseAll(name);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<CanonicalReference>();
    }

    [TestMethod]
    public void ParseAll_HandlesDigests()
    {
        var name = $"sha256:{new string('a', 64)}";

        var result = DockerReferenceUtility.ParseAll(name);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<CanonicalReference>();
    }

    [TestMethod]
    public void ParseAll_ParsesFamiliarNames()
    {
        var name = "docker.io/library/nginx";

        var result = DockerReferenceUtility.ParseAll(name);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<RepositoryReference>();
    }

    [TestMethod]
    public void HasUnresolvedVariables_ReturnsTrueForDollarSign()
    {
        DockerReferenceUtility.HasUnresolvedVariables("${MY_IMAGE}:latest").Should().BeTrue();
    }

    [TestMethod]
    public void HasUnresolvedVariables_ReturnsTrueForBraces()
    {
        DockerReferenceUtility.HasUnresolvedVariables("{{ .Values.image }}").Should().BeTrue();
    }

    [TestMethod]
    public void HasUnresolvedVariables_ReturnsFalseForPlainReference()
    {
        DockerReferenceUtility.HasUnresolvedVariables("docker.io/library/nginx:latest").Should().BeFalse();
    }

    [TestMethod]
    public void TryParseImageReference_ReturnsNullForUnresolvedVariables()
    {
        DockerReferenceUtility.TryParseImageReference("${IMAGE}:latest").Should().BeNull();
    }

    [TestMethod]
    public void TryParseImageReference_ThrowsForInvalidReference()
    {
        var func = () => DockerReferenceUtility.TryParseImageReference("docker.io/library/Nginx");

        func.Should().Throw<ReferenceNameContainsUppercaseException>();
    }

    [TestMethod]
    public void TryParseImageReference_ReturnsParsedReferenceForValidInput()
    {
        var result = DockerReferenceUtility.TryParseImageReference("nginx:latest");

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<TaggedReference>();
    }

    [TestMethod]
    public void TryParseImageReference_ReturnsParsedReferenceForDigest()
    {
        var digest = $"sha256:{new string('a', 64)}";
        var result = DockerReferenceUtility.TryParseImageReference($"nginx@{digest}");

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<CanonicalReference>();
        ((CanonicalReference)result).Digest.Should().Be(digest);
    }

    [TestMethod]
    public void TryParseImageReference_ReturnsParsedReferenceForTagAndDigest()
    {
        var digest = $"sha256:{new string('a', 64)}";
        var result = DockerReferenceUtility.TryParseImageReference($"nginx:latest@{digest}");

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<DualReference>();
        var dualRef = (DualReference)result;
        dualRef.Tag.Should().Be("latest");
        dualRef.Digest.Should().Be(digest);
    }

    [TestMethod]
    public void TryRegisterImageReference_RegistersValidReference()
    {
        var recorder = new Mock<ISingleFileComponentRecorder>();

        DockerReferenceUtility.TryRegisterImageReference("nginx:latest", recorder.Object);

        recorder.Verify(r => r.RegisterUsage(It.IsAny<DetectedComponent>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DependencyScope?>(), It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public void TryRegisterImageReference_SkipsUnresolvedVariables()
    {
        var recorder = new Mock<ISingleFileComponentRecorder>();

        DockerReferenceUtility.TryRegisterImageReference("${IMAGE}", recorder.Object);

        recorder.Verify(r => r.RegisterUsage(It.IsAny<DetectedComponent>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DependencyScope?>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void TryRegisterImageReference_ThrowsForInvalidReference()
    {
        var recorder = new Mock<ISingleFileComponentRecorder>();

        var func = () => DockerReferenceUtility.TryRegisterImageReference("docker.io/library/Nginx", recorder.Object);

        func.Should().Throw<ReferenceNameContainsUppercaseException>();
        recorder.Verify(r => r.RegisterUsage(It.IsAny<DetectedComponent>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<DependencyScope?>(), It.IsAny<string>()), Times.Never);
    }
}

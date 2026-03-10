namespace Microsoft.ComponentDetection.Detectors.Tests;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ImageReferenceTests
{
    [TestMethod]
    public void Parse_DockerImage_ReturnsDockerImageKind()
    {
        var result = ImageReference.Parse("node:latest");

        result.Kind.Should().Be(ImageReferenceKind.DockerImage);
        result.OriginalInput.Should().Be("node:latest");
        result.Reference.Should().Be("node:latest");
    }

    [TestMethod]
    public void Parse_DockerImage_LowercasesReference()
    {
        var result = ImageReference.Parse("MyImage:Latest");

        result.Kind.Should().Be(ImageReferenceKind.DockerImage);
        result.OriginalInput.Should().Be("MyImage:Latest");
        result.Reference.Should().Be("myimage:latest");
    }

    [TestMethod]
    public void Parse_DockerImageSha_ReturnsDockerImageKind()
    {
        var result = ImageReference.Parse("sha256:abc123def456");

        result.Kind.Should().Be(ImageReferenceKind.DockerImage);
        result.OriginalInput.Should().Be("sha256:abc123def456");
        result.Reference.Should().Be("sha256:abc123def456");
    }

    [TestMethod]
    public void Parse_OciDir_ReturnsOciLayoutKind()
    {
        var result = ImageReference.Parse("oci-dir:/path/to/image");

        result.Kind.Should().Be(ImageReferenceKind.OciLayout);
        result.OriginalInput.Should().Be("oci-dir:/path/to/image");
        result.Reference.Should().Be("/path/to/image");
    }

    [TestMethod]
    public void Parse_OciDir_PreservesPathCase()
    {
        var result = ImageReference.Parse("oci-dir:/Path/To/Image");

        result.Kind.Should().Be(ImageReferenceKind.OciLayout);
        result.OriginalInput.Should().Be("oci-dir:/Path/To/Image");
        result.Reference.Should().Be("/Path/To/Image");
    }

    [TestMethod]
    public void Parse_OciDirCaseInsensitivePrefix_ReturnsOciLayoutKind()
    {
        var result = ImageReference.Parse("OCI-DIR:/path/to/image");

        result.Kind.Should().Be(ImageReferenceKind.OciLayout);
        result.OriginalInput.Should().Be("OCI-DIR:/path/to/image");
        result.Reference.Should().Be("/path/to/image");
    }

    [TestMethod]
    public void Parse_OciArchive_ReturnsOciArchiveKind()
    {
        var result = ImageReference.Parse("oci-archive:/path/to/image.tar");

        result.Kind.Should().Be(ImageReferenceKind.OciArchive);
        result.OriginalInput.Should().Be("oci-archive:/path/to/image.tar");
        result.Reference.Should().Be("/path/to/image.tar");
    }

    [TestMethod]
    public void Parse_OciArchive_PreservesPathCase()
    {
        var result = ImageReference.Parse("oci-archive:/Path/To/Image.tar");

        result.Kind.Should().Be(ImageReferenceKind.OciArchive);
        result.OriginalInput.Should().Be("oci-archive:/Path/To/Image.tar");
        result.Reference.Should().Be("/Path/To/Image.tar");
    }

    [TestMethod]
    public void Parse_OciArchiveCaseInsensitivePrefix_ReturnsOciArchiveKind()
    {
        var result = ImageReference.Parse("OCI-ARCHIVE:/path/to/image.tar");

        result.Kind.Should().Be(ImageReferenceKind.OciArchive);
        result.OriginalInput.Should().Be("OCI-ARCHIVE:/path/to/image.tar");
        result.Reference.Should().Be("/path/to/image.tar");
    }
}

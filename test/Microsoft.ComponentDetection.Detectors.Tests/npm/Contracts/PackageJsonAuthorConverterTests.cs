#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Npm.Contracts;

using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PackageJsonAuthorConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [TestMethod]
    public void ParsesStringWithNameOnly()
    {
        var json = """{ "author": "John Doe" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().BeNull();
        result.Author.Url.Should().BeNull();
    }

    [TestMethod]
    public void ParsesStringWithNameAndEmail()
    {
        var json = """{ "author": "John Doe <john@example.com>" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().Be("john@example.com");
    }

    [TestMethod]
    public void ParsesStringWithNameEmailAndUrl()
    {
        var json = """{ "author": "John Doe <john@example.com> (https://example.com)" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().Be("john@example.com");
    }

    [TestMethod]
    public void ParsesStringWithNameAndUrl_NoEmail()
    {
        var json = @"{ ""author"": ""John Doe (https://example.com)"" }";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().BeNull();
    }

    [TestMethod]
    public void ParsesObjectFormat()
    {
        var json = """{ "author": { "name": "John Doe", "email": "john@example.com", "url": "https://example.com" } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().Be("john@example.com");
        result.Author.Url.Should().Be("https://example.com");
    }

    [TestMethod]
    public void ParsesObjectWithNameOnly()
    {
        var json = """{ "author": { "name": "John Doe" } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().BeNull();
        result.Author.Url.Should().BeNull();
    }

    [TestMethod]
    public void ReturnsNullForNullValue()
    {
        var json = """{ "author": null }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().BeNull();
    }

    [TestMethod]
    public void ReturnsNullForEmptyString()
    {
        var json = """{ "author": "" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().BeNull();
    }

    [TestMethod]
    public void ReturnsNullForWhitespaceOnlyString()
    {
        var json = """{ "author": "   " }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().BeNull();
    }

    [TestMethod]
    public void HandlesStringWithExtraWhitespace()
    {
        var json = """{ "author": "  John Doe   <john@example.com>  " }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Author.Email.Should().Be("john@example.com");
    }

    [TestMethod]
    public void HandlesMissingAuthorField()
    {
        var json = """{ "name": "test-package" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().BeNull();
    }

    [TestMethod]
    public void SkipsUnexpectedTokenTypes()
    {
        // Author as a number (malformed)
        var json = """{ "author": 12345 }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Author.Should().BeNull();
    }

    [TestMethod]
    public void CanSerializeAuthorObject()
    {
        var packageJson = new PackageJson
        {
            Author = new PackageJsonAuthor
            {
                Name = "John Doe",
                Email = "john@example.com",
            },
        };

        var json = JsonSerializer.Serialize(packageJson, Options);
        var deserialized = JsonSerializer.Deserialize<PackageJson>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized.Author.Should().NotBeNull();
        deserialized.Author.Name.Should().Be("John Doe");
        deserialized.Author.Email.Should().Be("john@example.com");
    }

    [TestMethod]
    public void CanSerializeNullAuthor()
    {
        var packageJson = new PackageJson
        {
            Name = "test-package",
            Author = null,
        };

        var json = JsonSerializer.Serialize(packageJson, Options);
        var deserialized = JsonSerializer.Deserialize<PackageJson>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized.Author.Should().BeNull();
    }
}

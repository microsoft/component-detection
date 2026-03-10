#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Npm.Contracts;

using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Integration tests for PackageJson deserialization using all converters together.
/// </summary>
[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PackageJsonConvertersTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [TestMethod]
    public void ParseCompletePackageJson()
    {
        var json = """
            {
                "name": "test-package",
                "version": "1.0.0",
                "author": "John Doe <john@example.com>",
                "engines": { "node": ">=14.0.0" },
                "workspaces": ["packages/*"]
            }
            """;

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Name.Should().Be("test-package");
        result.Version.Should().Be("1.0.0");
        result.Author.Should().NotBeNull();
        result.Author.Name.Should().Be("John Doe");
        result.Engines.Should().NotBeNull();
        result.Engines["node"].Should().Be(">=14.0.0");
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().Contain("packages/*");
    }

    [TestMethod]
    public void HandleAllNullableFields()
    {
        var json = """
            {
                "name": "test-package",
                "author": null,
                "engines": null,
                "workspaces": null
            }
            """;

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Name.Should().Be("test-package");
        result.Author.Should().BeNull();
        result.Engines.Should().BeNull();
        result.Workspaces.Should().BeNull();
    }

    [TestMethod]
    public void HandleMinimalPackageJson()
    {
        var json = """{ "name": "minimal" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Name.Should().Be("minimal");
        result.Author.Should().BeNull();
        result.Engines.Should().BeNull();
        result.Workspaces.Should().BeNull();
    }
}

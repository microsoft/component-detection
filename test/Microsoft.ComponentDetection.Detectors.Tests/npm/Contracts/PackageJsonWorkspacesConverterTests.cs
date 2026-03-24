#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Npm.Contracts;

using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PackageJsonWorkspacesConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [TestMethod]
    public void ParsesArrayFormat()
    {
        var json = """{ "workspaces": ["packages/*", "apps/*"] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().HaveCount(2);
        result.Workspaces.Should().Contain("packages/*");
        result.Workspaces.Should().Contain("apps/*");
    }

    [TestMethod]
    public void ParsesSingleWorkspace()
    {
        var json = """{ "workspaces": ["packages/*"] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().HaveCount(1);
        result.Workspaces.Should().Contain("packages/*");
    }

    [TestMethod]
    public void ParsesEmptyArray()
    {
        var json = """{ "workspaces": [] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().BeEmpty();
    }

    [TestMethod]
    public void ParsesObjectWithPackagesField()
    {
        var json = """{ "workspaces": { "packages": ["packages/*", "apps/*"] } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().HaveCount(2);
        result.Workspaces.Should().Contain("packages/*");
        result.Workspaces.Should().Contain("apps/*");
    }

    [TestMethod]
    public void ParsesObjectWithEmptyPackagesArray()
    {
        var json = """{ "workspaces": { "packages": [] } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().BeEmpty();
    }

    [TestMethod]
    public void ReturnsNullForObjectWithoutPackages()
    {
        // Object format without packages field
        var json = """{ "workspaces": { "nohoist": ["**/react-native"] } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().BeNull();
    }

    [TestMethod]
    public void ReturnsNullForNullValue()
    {
        var json = """{ "workspaces": null }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().BeNull();
    }

    [TestMethod]
    public void HandlesMissingWorkspacesField()
    {
        var json = """{ "name": "test-package" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().BeNull();
    }

    [TestMethod]
    public void SkipsUnexpectedTokenTypes()
    {
        // Workspaces as a string (malformed)
        var json = """{ "workspaces": "packages/*" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().BeNull();
    }

    [TestMethod]
    public void IgnoresNonStringValuesInArray()
    {
        var json = """{ "workspaces": ["packages/*", 123, "apps/*"] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().HaveCount(2);
        result.Workspaces.Should().Contain("packages/*");
        result.Workspaces.Should().Contain("apps/*");
    }

    [TestMethod]
    public void HandlesObjectWithOtherFieldsAndPackages()
    {
        // Yarn workspaces can have both packages and nohoist fields
        var json = """{ "workspaces": { "packages": ["packages/*"], "nohoist": ["**/react-native"] } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().HaveCount(1);
        result.Workspaces.Should().Contain("packages/*");
    }

    [TestMethod]
    public void CanSerializeWorkspaces()
    {
        var packageJson = new PackageJson
        {
            Workspaces = ["packages/*", "apps/*"],
        };

        var json = JsonSerializer.Serialize(packageJson, Options);
        var deserialized = JsonSerializer.Deserialize<PackageJson>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized.Workspaces.Should().NotBeNull();
        deserialized.Workspaces.Should().HaveCount(2);
        deserialized.Workspaces.Should().Contain("packages/*");
        deserialized.Workspaces.Should().Contain("apps/*");
    }

    [TestMethod]
    public void CanSerializeNullWorkspaces()
    {
        var packageJson = new PackageJson
        {
            Name = "test-package",
            Workspaces = null,
        };

        var json = JsonSerializer.Serialize(packageJson, Options);
        var deserialized = JsonSerializer.Deserialize<PackageJson>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized.Workspaces.Should().BeNull();
    }

    [TestMethod]
    public void IsCaseInsensitiveForPackagesField()
    {
        var json = """{ "workspaces": { "PACKAGES": ["packages/*"] } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Workspaces.Should().NotBeNull();
        result.Workspaces.Should().HaveCount(1);
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PackageJsonEnginesConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [TestMethod]
    public void ParsesObjectFormat()
    {
        var json = """{ "engines": { "node": ">=14.0.0", "npm": ">=6.0.0" } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();
        result.Engines.Should().HaveCount(2);
        result.Engines["node"].Should().Be(">=14.0.0");
        result.Engines["npm"].Should().Be(">=6.0.0");
    }

    [TestMethod]
    public void ParsesSingleEngine()
    {
        var json = """{ "engines": { "node": "^16.0.0" } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();
        result.Engines.Should().HaveCount(1);
        result.Engines["node"].Should().Be("^16.0.0");
    }

    [TestMethod]
    public void ParsesEmptyObject()
    {
        var json = """{ "engines": {} }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();
        result.Engines.Should().BeEmpty();
    }

    [TestMethod]
    public void ReturnsNullForNullValue()
    {
        var json = """{ "engines": null }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().BeNull();
    }

    [TestMethod]
    public void HandlesMalformedArrayFormat()
    {
        // Some malformed package.json files have engines as an array
        var json = """{ "engines": ["node >= 14"] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();

        // Array format returns empty dictionary since we can't map to key-value pairs
        result.Engines.Should().BeEmpty();
    }

    [TestMethod]
    public void HandlesArrayWithVscodeEngine()
    {
        // When array contains vscode, it should be captured
        var json = """{ "engines": ["vscode ^1.60.0", "node >= 14"] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();
        result.Engines.Should().ContainKey("vscode");
        result.Engines["vscode"].Should().Be("vscode ^1.60.0");
    }

    [TestMethod]
    public void HandlesArrayWithVscodeUpperCase()
    {
        var json = """{ "engines": ["VSCODE >= 1.0.0"] }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();
        result.Engines.Should().ContainKey("vscode");
    }

    [TestMethod]
    public void SkipsNonStringValuesInObject()
    {
        // If a value is not a string, it should be skipped
        var json = """{ "engines": { "node": ">=14.0.0", "invalid": 123 } }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().NotBeNull();
        result.Engines.Should().HaveCount(1);
        result.Engines["node"].Should().Be(">=14.0.0");
    }

    [TestMethod]
    public void HandlesMissingEnginesField()
    {
        var json = """{ "name": "test-package" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().BeNull();
    }

    [TestMethod]
    public void SkipsUnexpectedTokenTypes()
    {
        // Engines as a string (malformed)
        var json = """{ "engines": "node >= 14" }""";

        var result = JsonSerializer.Deserialize<PackageJson>(json, Options);

        result.Should().NotBeNull();
        result.Engines.Should().BeNull();
    }

    [TestMethod]
    public void CanSerializeEngines()
    {
        var packageJson = new PackageJson
        {
            Engines = new Dictionary<string, string>
            {
                ["node"] = ">=14.0.0",
                ["npm"] = ">=6.0.0",
            },
        };

        var json = JsonSerializer.Serialize(packageJson, Options);
        var deserialized = JsonSerializer.Deserialize<PackageJson>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized.Engines.Should().NotBeNull();
        deserialized.Engines.Should().HaveCount(2);
        deserialized.Engines["node"].Should().Be(">=14.0.0");
        deserialized.Engines["npm"].Should().Be(">=6.0.0");
    }

    [TestMethod]
    public void CanSerializeNullEngines()
    {
        var packageJson = new PackageJson
        {
            Name = "test-package",
            Engines = null,
        };

        var json = JsonSerializer.Serialize(packageJson, Options);
        var deserialized = JsonSerializer.Deserialize<PackageJson>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized.Engines.Should().BeNull();
    }
}

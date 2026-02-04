namespace Microsoft.ComponentDetection.Detectors.Tests.Npm.Contracts;

using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Npm.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PackageLockPackageEnginesTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [TestMethod]
    public void PackageLockV2Package_ParsesEnginesAsObject()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": { "node": ">=14.0.0", "npm": ">=6.0.0" }
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV2Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().NotBeNull();
        result.Engines.Should().HaveCount(2);
        result.Engines!["node"].Should().Be(">=14.0.0");
        result.Engines["npm"].Should().Be(">=6.0.0");
    }

    [TestMethod]
    public void PackageLockV2Package_ParsesEnginesAsArray()
    {
        // Legacy format: engines as array (e.g., concat-stream package)
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": ["node >= 0.8"]
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV2Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().NotBeNull();

        // Array format returns empty dictionary since we can't map to key-value pairs
        result.Engines.Should().BeEmpty();
    }

    [TestMethod]
    public void PackageLockV2Package_ParsesEnginesArrayWithVscode()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": ["vscode ^1.60.0", "node >= 14"]
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV2Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().NotBeNull();
        result.Engines.Should().ContainKey("vscode");
        result.Engines!["vscode"].Should().Be("vscode ^1.60.0");
    }

    [TestMethod]
    public void PackageLockV2Package_ParsesNullEngines()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": null
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV2Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().BeNull();
    }

    [TestMethod]
    public void PackageLockV2Package_ParsesMissingEngines()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123"
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV2Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().BeNull();
    }

    [TestMethod]
    public void PackageLockV3Package_ParsesEnginesAsObject()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": { "node": ">=16.0.0", "npm": ">=8.0.0" }
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV3Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().NotBeNull();
        result.Engines.Should().HaveCount(2);
        result.Engines!["node"].Should().Be(">=16.0.0");
        result.Engines["npm"].Should().Be(">=8.0.0");
    }

    [TestMethod]
    public void PackageLockV3Package_ParsesEnginesAsArray()
    {
        // Legacy format: engines as array (e.g., concat-stream package)
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": ["node >= 0.8"]
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV3Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().NotBeNull();

        // Array format returns empty dictionary since we can't map to key-value pairs
        result.Engines.Should().BeEmpty();
    }

    [TestMethod]
    public void PackageLockV3Package_ParsesEnginesArrayWithVscode()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": ["vscode ^1.60.0", "node >= 14"]
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV3Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().NotBeNull();
        result.Engines.Should().ContainKey("vscode");
        result.Engines!["vscode"].Should().Be("vscode ^1.60.0");
    }

    [TestMethod]
    public void PackageLockV3Package_ParsesNullEngines()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123",
            "engines": null
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV3Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().BeNull();
    }

    [TestMethod]
    public void PackageLockV3Package_ParsesMissingEngines()
    {
        var json = """
        {
            "version": "1.0.0",
            "resolved": "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            "integrity": "sha512-abc123"
        }
        """;

        var result = JsonSerializer.Deserialize<PackageLockV3Package>(json, Options);

        result.Should().NotBeNull();
        result!.Engines.Should().BeNull();
    }

    [TestMethod]
    public void PackageLockV2Package_CanSerializeEngines()
    {
        var package = new PackageLockV2Package
        {
            Version = "1.0.0",
            Resolved = "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            Integrity = "sha512-abc123",
            Engines = new Dictionary<string, string>
            {
                ["node"] = ">=14.0.0",
                ["npm"] = ">=6.0.0",
            },
        };

        var json = JsonSerializer.Serialize(package, Options);
        var deserialized = JsonSerializer.Deserialize<PackageLockV2Package>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Engines.Should().NotBeNull();
        deserialized.Engines.Should().HaveCount(2);
        deserialized.Engines!["node"].Should().Be(">=14.0.0");
        deserialized.Engines["npm"].Should().Be(">=6.0.0");
    }

    [TestMethod]
    public void PackageLockV3Package_CanSerializeEngines()
    {
        var package = new PackageLockV3Package
        {
            Version = "1.0.0",
            Resolved = "https://registry.npmjs.org/test/-/test-1.0.0.tgz",
            Integrity = "sha512-abc123",
            Engines = new Dictionary<string, string>
            {
                ["node"] = ">=16.0.0",
                ["npm"] = ">=8.0.0",
            },
        };

        var json = JsonSerializer.Serialize(package, Options);
        var deserialized = JsonSerializer.Deserialize<PackageLockV3Package>(json, Options);

        deserialized.Should().NotBeNull();
        deserialized!.Engines.Should().NotBeNull();
        deserialized.Engines.Should().HaveCount(2);
        deserialized.Engines!["node"].Should().Be(">=16.0.0");
        deserialized.Engines["npm"].Should().Be(">=8.0.0");
    }
}

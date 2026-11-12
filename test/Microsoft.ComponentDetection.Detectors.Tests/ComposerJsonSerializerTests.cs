#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Composer.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComposerJsonSerializerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [TestMethod]
    public void DeserializeLockFile_PreservesTypedMetadataAndUsesLastDuplicateValue()
    {
        var file = JsonSerializer.Deserialize<ComposerLockFile>(
            """
            {
              "packages":[{
                "name":" Vendor/App ","version":"dev-main",
                "require":{"vendor/api":"1","vendor/api":"2"},
                "extra":{"branch-alias":{"dev-main":"2.x-dev"}}
              }],
              "packages-dev":[{"name":"vendor/tests","version":"3"}],
              "aliases":[{"package":"Vendor/App","version":"dev-main","alias":"2.x-dev"}]
            }
            """,
            SerializerOptions);

        var package = file.Packages.Should().ContainSingle().Which;
        package.Name.Should().Be(" Vendor/App ");
        package.Version.Should().Be("dev-main");
        package.Require.Should().ContainSingle().Which.Key.Should().Be("vendor/api");
        package.Require["vendor/api"].Should().Be("2");
        package.Extra.BranchAliases.Should().ContainSingle().Which.Value.Should().Be("2.x-dev");
        file.DevelopmentPackages.Should().ContainSingle().Which.Name.Should().Be("vendor/tests");
        var alias = file.Aliases.Should().ContainSingle().Which;
        alias.Package.Should().Be("Vendor/App");
        alias.Version.Should().Be("dev-main");
        alias.Alias.Should().Be("2.x-dev");
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{}")]
    public void DeserializeLockFile_NullOrMissingEntryPropertiesDoNotDiscardFollowingPackages(string invalidEntry)
    {
        var file = JsonSerializer.Deserialize<ComposerLockFile>(
            $$$"""
            {"packages":[{{{invalidEntry}}},{"name":"vendor/valid","version":"1"}]}
            """,
            SerializerOptions);

        file.Packages.Should().HaveCount(2);
        file.Packages[0]?.Name.Should().BeNull();
        file.Packages[1].Name.Should().Be("vendor/valid");
        file.Packages[1].Version.Should().Be("1");
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"config\":null}")]
    public void DeserializeManifest_MissingOrNullConfigIsUnavailable(string json)
    {
        var manifest = JsonSerializer.Deserialize<ComposerManifest>(json, SerializerOptions);

        manifest.Config.Should().BeNull();
    }

    [TestMethod]
    [DataRow("{\"config\":false}")]
    [DataRow("{\"config\":[]}")]
    public void DeserializeManifest_MalformedConfigReportsJsonError(string json)
    {
        Action deserialize = () => JsonSerializer.Deserialize<ComposerManifest>(json, SerializerOptions);

        deserialize.Should().Throw<JsonException>();
    }

    [TestMethod]
    [DataRow("{\"config\":{}}")]
    [DataRow("{\"config\":{\"vendor-dir\":null}}")]
    public void DeserializeManifest_MissingOrNullVendorDirectoryIsUnavailable(string json)
    {
        var manifest = JsonSerializer.Deserialize<ComposerManifest>(json, SerializerOptions);

        manifest.Config.Should().NotBeNull();
        manifest.Config.VendorDirectory.Should().BeNull();
    }

    [TestMethod]
    public void DeserializeInstalledPackages_ReadsCurrentInventory()
    {
        const string packages = """[{"name":"vendor/package","version":"1"}]""";
        var json = $$$"""{"packages":{{{packages}}},"dev-package-names":["vendor/package"]}""";
        var inventory = JsonSerializer.Deserialize<ComposerInstalledPackages>(json, SerializerOptions);

        var package = inventory.Packages.Should().ContainSingle().Which;
        package.Name.Should().Be("vendor/package");
        package.Version.Should().Be("1");
        inventory.DevelopmentPackageNames.Should().Equal("vendor/package");
    }

    [TestMethod]
    [DataRow("{\"packages\":[{\"name\":\"vendor/app\",\"version\":\"1\"}")]
    [DataRow("{\"packages\":[false,}")]
    [DataRow("{\"packages\":{\"unexpected\":[}")]
    [DataRow("{\"packages\":[{\"require\":{\"vendor/child\":[}}]}")]
    [DataRow("{\"packages\":[false]}")]
    [DataRow("{\"packages\":[42]}")]
    [DataRow("{\"packages\":[[]]}")]
    [DataRow("{\"packages\":[\"invalid\"]}")]
    public void DeserializeLockFile_DoesNotTreatInvalidJsonAsRecoverableSchemaErrors(string json)
    {
        Action deserialize = () => JsonSerializer.Deserialize<ComposerLockFile>(json, SerializerOptions);

        deserialize.Should().Throw<JsonException>();
    }

    [TestMethod]
    public async Task DeserializeAsync_ReadsFieldsAcrossSerializerBufferBoundariesAsync()
    {
        var json = $$$"""
            {"ignored":"{{{new string('x', 65536)}}}","packages":[
              {"name":"vendor/app","version":"1","require":{"vendor/child":"^2"}}
            ],"packages-dev":[{"name":"vendor/child","version":"2"}]}
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var file = await JsonSerializer.DeserializeAsync<ComposerLockFile>(stream, SerializerOptions);

        file.Packages.Should().ContainSingle().Which.Name.Should().Be("vendor/app");
        file.Packages[0].Require.Should().ContainSingle().Which.Value.Should().Be("^2");
        file.DevelopmentPackages.Should().ContainSingle().Which.Name.Should().Be("vendor/child");
        stream.CanRead.Should().BeTrue();
    }
}

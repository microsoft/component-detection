#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComposerComponentTests
{
    [TestMethod]
    public void ParameterlessConstructor_LeavesIdentityUninitialized()
    {
        var component = new ComposerComponent();

        component.Name.Should().BeNull();
        component.Version.Should().BeNull();
        component.Type.Should().Be(ComponentType.Composer);
    }

    [TestMethod]
    [DataRow(" v1.2.3 ")]
    [DataRow("V2.0.0")]
    [DataRow(" dev-main ")]
    [DataRow("1.x-dev")]
    [DataRow("vnext")]
    [DataRow("dev-main#abc123")]
    [DataRow("v1.0.0-beta.1+build")]
    public void Identity_PreservesRawFieldsAndBaseId(string version)
    {
        var component = new ComposerComponent(" Vendor/Package ", version);

        component.Name.Should().Be(" Vendor/Package ");
        component.Version.Should().Be(version);
        component.Id.Should().Be($" Vendor/Package  {version} - Composer");
        component.BaseId.Should().Be(component.Id);
        component.Type.Should().Be(ComponentType.Composer);
        ((byte)component.Type).Should().Be(21);
    }

    [TestMethod]
    [DataRow(null, "1.0")]
    [DataRow("vendor/package", null)]
    public void Constructor_RejectsMissingInputs(string name, string version)
    {
        Action action = () => new ComposerComponent(name, version);
        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Constructor_DoesNotEnforceManifestNameGrammar()
    {
        var component = new ComposerComponent("Vendor--Name/Package", "1.0");

        component.Name.Should().Be("Vendor--Name/Package");
        component.Id.Should().Be("Vendor--Name/Package 1.0 - Composer");
        component.PackageUrl.ToString().Should().Be("pkg:composer/vendor--name/package@1.0");
    }

    [TestMethod]
    [DataRow("vendor/package--name", "vendor/package--name")]
    [DataRow("Vendor/Package--Name", "vendor/package--name")]
    [DataRow("vendor-name/package-name", "vendor-name/package-name")]
    [DataRow("vendor.name_with-hyphen/package.test_other--name12", "vendor.name_with-hyphen/package.test_other--name12")]
    [DataRow("0/1", "0/1")]
    public void Name_IsPreservedInComponentAndBaseId(string name, string expected)
    {
        var component = new ComposerComponent(name, "v1.2.3");

        component.Name.Should().Be(name);
        component.Version.Should().Be("v1.2.3");
        component.Id.Should().Be($"{name} v1.2.3 - Composer");
        component.PackageUrl.ToString().Should().Be($"pkg:composer/{expected}@v1.2.3");
    }

    [TestMethod]
    public void DifferentRawIdentities_AreNotNormalizedToTheSameBaseId()
    {
        var original = new ComposerComponent("Vendor/Package", "v1.2.3");
        var different = new ComposerComponent("vendor/package", "1.2.3");

        original.BaseId.Should().Be("Vendor/Package v1.2.3 - Composer");
        different.BaseId.Should().Be("vendor/package 1.2.3 - Composer");
        original.Id.Should().NotBe(different.Id);
        original.PackageUrl.Version.Should().Be("v1.2.3");
        different.PackageUrl.Version.Should().Be("1.2.3");
    }

    [TestMethod]
    public void PackageUrl_UsesVendorNamespaceAndPackageName()
    {
        var component = new ComposerComponent("Monolog/Monolog", "v3.9.0");
        component.PackageUrl.Type.Should().Be("composer");
        component.PackageUrl.Namespace.Should().Be("monolog");
        component.PackageUrl.Name.Should().Be("monolog");
        component.PackageUrl.Version.Should().Be("v3.9.0");
        component.PackageUrl.ToString().Should().Be("pkg:composer/monolog/monolog@v3.9.0");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolymorphicJson_RoundTripsIdentityAndMetadata(bool newtonsoft)
    {
        TypedComponent component = new ComposerComponent("Vendor/Package", "v1.2.3")
        {
            Licenses = ["MIT"],
        };
        var json = newtonsoft ? JsonConvert.SerializeObject(component) : System.Text.Json.JsonSerializer.Serialize(component);
        var restored = newtonsoft ? JsonConvert.DeserializeObject<TypedComponent>(json) : System.Text.Json.JsonSerializer.Deserialize<TypedComponent>(json);

        restored.Should().BeOfType<ComposerComponent>().Which.Should().BeEquivalentTo(component);
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("type").GetString().Should().Be("Composer");
        document.RootElement.GetProperty("name").GetString().Should().Be("Vendor/Package");
        document.RootElement.GetProperty("version").GetString().Should().Be("v1.2.3");
        document.RootElement.GetProperty("id").GetString().Should().Be("Vendor/Package v1.2.3 - Composer");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScanTransport_UsesFlatDiscriminatedComponent(bool newtonsoft)
    {
        var scanned = new ScannedComponent
        {
            DetectorId = "Composer",
            Component = new ComposerComponent("vendor/package", "dev-main"),
            IsDevelopmentDependency = true,
        };
        var json = newtonsoft ? JsonConvert.SerializeObject(scanned) : System.Text.Json.JsonSerializer.Serialize(scanned);
        var restored = newtonsoft ? JsonConvert.DeserializeObject<ScannedComponent>(json) : System.Text.Json.JsonSerializer.Deserialize<ScannedComponent>(json);

        restored.Component.Should().BeOfType<ComposerComponent>();
        restored.IsDevelopmentDependency.Should().BeTrue();
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("component");
        payload.GetProperty("type").GetString().Should().Be("Composer");
        payload.GetProperty("name").GetString().Should().Be("vendor/package");
        payload.TryGetProperty("composer", out _).Should().BeFalse();
        var packageUrl = payload.GetProperty("packageUrl");
        packageUrl.GetProperty("Type").GetString().Should().Be("composer");
        packageUrl.GetProperty("Namespace").GetString().Should().Be("vendor");
        packageUrl.GetProperty("Name").GetString().Should().Be("package");
    }

    [TestMethod]
    public void IdentityProperties_CanBeInitializedForDeserialization()
    {
        var component = new ComposerComponent
        {
            Name = "vendor/package",
            Version = "v1.2.3",
        };

        component.Name.Should().Be("vendor/package");
        component.Version.Should().Be("v1.2.3");
        component.Id.Should().Be("vendor/package v1.2.3 - Composer");
        component.PackageUrl.ToString().Should().Be("pkg:composer/vendor/package@v1.2.3");
    }
}

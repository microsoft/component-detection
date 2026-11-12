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
public class PhpSdkComponentTests
{
    [TestMethod]
    public void ParameterlessConstructor_LeavesIdentityUninitialized()
    {
        var component = new PhpSdkComponent();

        component.Name.Should().BeNull();
        component.Version.Should().BeNull();
        component.Type.Should().Be(ComponentType.PhpSdk);
    }

    [TestMethod]
    public void IdentityProperties_CanBeInitializedForDeserialization()
    {
        var component = new PhpSdkComponent
        {
            Name = "php",
            Version = "8.4.27",
        };

        component.Name.Should().Be("php");
        component.Version.Should().Be("8.4.27");
        component.Id.Should().Be("php 8.4.27 - PhpSdk");
        component.PackageUrl.ToString().Should().Be("pkg:generic/php@8.4.27?type=phpsdk");
    }

    [TestMethod]
    [DataRow("php", "8.4.27")]
    [DataRow("ext-openssl", "8.4.27")]
    [DataRow("lib-openssl", "3.5.0")]
    [DataRow("composer", "2.11.1")]
    public void IdentityAndPackageUrl_DescribeTheObservedEnvironment(string name, string version)
    {
        var component = new PhpSdkComponent(name, version);

        component.Name.Should().Be(name);
        component.Version.Should().Be(version);
        component.Type.Should().Be(ComponentType.PhpSdk);
        ((byte)component.Type).Should().Be(22);
        component.Id.Should().Be($"{name} {version} - PhpSdk");
        component.PackageUrl.ToString().Should().Be($"pkg:generic/{name}@{version}?type=phpsdk");
    }

    [TestMethod]
    [DataRow(null, "1.0")]
    [DataRow("", "1.0")]
    [DataRow("php", null)]
    [DataRow("php", " ")]
    public void Constructor_RejectsMissingIdentity(string name, string version)
    {
        Action action = () => new PhpSdkComponent(name, version);
        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PolymorphicJson_RoundTripsIdentityAndMetadata(bool newtonsoft)
    {
        TypedComponent component = new PhpSdkComponent("lib-curl", "8.12.1") { Licenses = ["MIT"] };
        var json = newtonsoft ? JsonConvert.SerializeObject(component) : System.Text.Json.JsonSerializer.Serialize(component);
        var restored = newtonsoft ? JsonConvert.DeserializeObject<TypedComponent>(json) : System.Text.Json.JsonSerializer.Deserialize<TypedComponent>(json);

        restored.Should().BeOfType<PhpSdkComponent>().Which.Should().BeEquivalentTo(component);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScanTransport_UsesThePhpSdkDiscriminator(bool newtonsoft)
    {
        var scanned = new ScannedComponent { DetectorId = "Composer", Component = new PhpSdkComponent("php", "8.4.27"), IsDevelopmentDependency = false };
        var json = newtonsoft ? JsonConvert.SerializeObject(scanned) : System.Text.Json.JsonSerializer.Serialize(scanned);
        var restored = newtonsoft ? JsonConvert.DeserializeObject<ScannedComponent>(json) : System.Text.Json.JsonSerializer.Deserialize<ScannedComponent>(json);

        restored.Component.Should().BeOfType<PhpSdkComponent>().Which.Version.Should().Be("8.4.27");
        restored.IsDevelopmentDependency.Should().BeFalse();
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("component");
        payload.GetProperty("type").GetString().Should().Be("PhpSdk");
        payload.GetProperty("name").GetString().Should().Be("php");
        payload.GetProperty("version").GetString().Should().Be("8.4.27");
        payload.GetProperty("packageUrl").GetProperty("Type").GetString().Should().Be("generic");
    }
}

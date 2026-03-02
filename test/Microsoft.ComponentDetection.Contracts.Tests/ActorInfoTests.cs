namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ActorInfoTests
{
    [TestMethod]
    public void ActorInfo_Serialization_AllProperties()
    {
        var actor = new ActorInfo
        {
            Name = "Test Person",
            Email = "test@example.com",
            Url = new Uri("https://example.com/test-person"),
            Type = "Person",
        };

        var json = JsonSerializer.Serialize(actor);
        var deserialized = JsonSerializer.Deserialize<ActorInfo>(json)!;

        deserialized.Name.Should().Be("Test Person");
        deserialized.Email.Should().Be("test@example.com");
        deserialized.Url.Should().Be(new Uri("https://example.com/test-person"));
        deserialized.Type.Should().Be("Person");
    }

    [TestMethod]
    public void ActorInfo_Serialization_PartialProperties()
    {
        var actor = new ActorInfo { Name = "Test Org", Type = "Organization" };

        var json = JsonSerializer.Serialize(actor);
        var deserialized = JsonSerializer.Deserialize<ActorInfo>(json)!;

        deserialized.Name.Should().Be("Test Org");
        deserialized.Email.Should().BeNull();
        deserialized.Url.Should().BeNull();
        deserialized.Type.Should().Be("Organization");
    }

    [TestMethod]
    public void ActorInfo_Serialization_NullPropertiesOmittedFromJson()
    {
        var actor = new ActorInfo { Name = "TestBot", Type = "SoftwareAgent" };

        var json = JsonSerializer.Serialize(actor);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.TryGetProperty("email", out _).Should().BeFalse();
        root.TryGetProperty("url", out _).Should().BeFalse();

        root.TryGetProperty("name", out var nameProperty).Should().BeTrue();
        nameProperty.ValueKind.Should().Be(JsonValueKind.String);

        root.TryGetProperty("type", out var typeProperty).Should().BeTrue();
        typeProperty.ValueKind.Should().Be(JsonValueKind.String);
    }

    [TestMethod]
    public void ActorInfo_Deserialization_FromPartialJson()
    {
        var json = """{"name":"Test Person","url":"https://example.com/test-person"}""";

        var deserialized = JsonSerializer.Deserialize<ActorInfo>(json)!;

        deserialized.Name.Should().Be("Test Person");
        deserialized.Url.Should().Be(new Uri("https://example.com/test-person"));
        deserialized.Email.Should().BeNull();
        deserialized.Type.Should().BeNull();
    }
}

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

    [TestMethod]
    public void ActorInfo_Equals_SameValues_ReturnsTrue()
    {
        var a = new ActorInfo { Name = "Alice", Email = "alice@example.com", Url = new Uri("https://example.com"), Type = "Person" };
        var b = new ActorInfo { Name = "Alice", Email = "alice@example.com", Url = new Uri("https://example.com"), Type = "Person" };

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void ActorInfo_Equals_CaseInsensitive_ReturnsTrue()
    {
        var a = new ActorInfo { Name = "alice", Email = "ALICE@EXAMPLE.COM", Type = "person" };
        var b = new ActorInfo { Name = "Alice", Email = "alice@example.com", Type = "Person" };

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void ActorInfo_Equals_DifferentValues_ReturnsFalse()
    {
        var a = new ActorInfo { Name = "Alice", Type = "Person" };
        var b = new ActorInfo { Name = "Bob", Type = "Person" };

        a.Equals(b).Should().BeFalse();
    }

    [TestMethod]
    public void ActorInfo_Equals_Null_ReturnsFalse()
    {
        var a = new ActorInfo { Name = "Alice" };
        ActorInfo? nullActor = null;

#pragma warning disable CA1508 // Avoid dead conditional code — intentionally testing null equality
        a.Equals(nullActor).Should().BeFalse();
#pragma warning restore CA1508
    }

    [TestMethod]
    public void ActorInfo_HashSet_DeduplicatesEquivalentEntries()
    {
        var a = new ActorInfo { Name = "Alice", Type = "Person" };
        var b = new ActorInfo { Name = "alice", Type = "PERSON" };
        var c = new ActorInfo { Name = "Bob", Type = "Organization" };

        var set = new System.Collections.Generic.HashSet<ActorInfo> { a, b, c };

        set.Should().HaveCount(2);
    }
}

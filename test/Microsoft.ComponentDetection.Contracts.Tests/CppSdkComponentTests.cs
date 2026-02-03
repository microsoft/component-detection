#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class CppSdkComponentTests
{
    [TestMethod]
    public void Constructor_ShouldInitializeProperties()
    {
        var name = "AwesomeCppLibrary";
        var version = "1.2.3";

        var component = new CppSdkComponent(name, version);

        component.Name.Should().Be(name);
        component.Version.Should().Be(version);
        component.Type.Should().Be(ComponentType.CppSdk);
        component.Id.Should().Be($"{name} {version} - {component.Type}");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenNameIsNull()
    {
        Action action = () => new CppSdkComponent(null, "1.2.3");
        action.Should().Throw<ArgumentException>().WithMessage("*Name*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenNameIsWhitespace()
    {
        Action action = () => new CppSdkComponent("   ", "1.2.3");
        action.Should().Throw<ArgumentException>().WithMessage("*Name*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenNameIsEmpty()
    {
        Action action = () => new CppSdkComponent(string.Empty, "1.2.3");
        action.Should().Throw<ArgumentException>().WithMessage("*Name*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenVersionIsNull()
    {
        Action action = () => new CppSdkComponent("AwesomeCppLibrary", null);
        action.Should().Throw<ArgumentException>().WithMessage("*Version*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenVersionIsWhitespace()
    {
        Action action = () => new CppSdkComponent("AwesomeCppLibrary", "   ");
        action.Should().Throw<ArgumentException>().WithMessage("*Version*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenVersionIsEmpty()
    {
        Action action = () => new CppSdkComponent("AwesomeCppLibrary", string.Empty);
        action.Should().Throw<ArgumentException>().WithMessage("*Version*");
    }

    [TestMethod]
    public void PackageUrl_ShouldReturnCorrectFormat()
    {
        var name = "AwesomeCppLibrary";
        var version = "1.2.3";
        var component = new CppSdkComponent(name, version);

        var packageUrl = component.PackageUrl;

        packageUrl.Type.Should().Be("generic");
#pragma warning disable CA1308 // PackageURL normalizes to lowercase
        packageUrl.Name.Should().Be(name.ToLowerInvariant());
#pragma warning restore CA1308
        packageUrl.Version.Should().Be(version);
        packageUrl.Namespace.Should().BeNull();
        packageUrl.Qualifiers.Should().ContainKey("type");
        packageUrl.Qualifiers["type"].Should().Be("cppsdk");
    }
}

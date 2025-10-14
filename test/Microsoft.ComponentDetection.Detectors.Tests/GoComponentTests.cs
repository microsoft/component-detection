#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class GoComponentTests
{
    private static readonly string TestName = Guid.NewGuid().ToString();
    private static readonly string TestVersion = Guid.NewGuid().ToString();
    private static readonly string TestHash = Guid.NewGuid().ToString();

    [TestInitialize]
    public void TestInitialize()
    {
    }

    [TestMethod]
    public void ConstructorTest_NameVersion()
    {
        var goComponent = new GoComponent(TestName, TestVersion);
        goComponent.Name.Should().Be(TestName);
        goComponent.Version.Should().Be(TestVersion);
        goComponent.Hash.Should().Be(string.Empty);
        goComponent.Id.Should().Be($"{TestName} {TestVersion} - Go");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MSTEST0006:Avoid '[ExpectedException]'", Justification = "Single-line test case")]
    public void ConstructorTest_NameVersion_NullVersion()
    {
        var goComponent = new GoComponent(TestName, null);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MSTEST0006:Avoid '[ExpectedException]'", Justification = "Single-line test case")]
    public void ConstructorTest_NameVersion_NullName()
    {
        var goComponent = new GoComponent(null, TestVersion);
    }

    [TestMethod]
    public void ConstructorTest_NameVersionHash()
    {
        var goComponent = new GoComponent(TestName, TestVersion, TestHash);
        goComponent.Name.Should().Be(TestName);
        goComponent.Version.Should().Be(TestVersion);
        goComponent.Hash.Should().Be(TestHash);
        goComponent.Id.Should().Be($"{TestName} {TestVersion} - Go");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MSTEST0006:Avoid '[ExpectedException]'", Justification = "Single-line test case")]
    public void ConstructorTest_NameVersionHash_NullVersion()
    {
        var goComponent = new GoComponent(TestName, null, TestHash);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MSTEST0006:Avoid '[ExpectedException]'", Justification = "Single-line test case")]
    public void ConstructorTest_NameVersionHash_NullName()
    {
        var goComponent = new GoComponent(null, TestVersion, TestHash);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MSTEST0006:Avoid '[ExpectedException]'", Justification = "Single-line test case")]
    public void ConstructorTest_NameVersionHash_NullHash()
    {
        var goComponent = new GoComponent(TestName, TestVersion, null);
    }

    [TestMethod]
    public void TestEquals()
    {
        var goComponent1 = new GoComponent(TestName, TestVersion, TestHash);
        var goComponent2 = new GoComponent(TestName, TestVersion, TestHash);
        var goComponent3 = new GoComponent(TestName, TestVersion, Guid.NewGuid().ToString());
        goComponent1.Equals(goComponent2).Should().BeTrue();
        ((object)goComponent1).Equals(goComponent2).Should().BeTrue();

        goComponent1.Equals(goComponent3).Should().BeFalse();
        ((object)goComponent1).Equals(goComponent3).Should().BeFalse();
    }

    [TestMethod]
    public void TestGetHashCode()
    {
        var goComponent1 = new GoComponent(TestName, TestVersion, TestHash);
        var goComponent2 = new GoComponent(TestName, TestVersion, TestHash);
        goComponent1.GetHashCode().Should().Be(goComponent2.GetHashCode());
    }
}

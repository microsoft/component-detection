#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class EnvironmentVariableServiceTests
{
    public const string MyEnvVar = nameof(MyEnvVar);
    private EnvironmentVariableService testSubject;

    [TestInitialize]
    public void TestInitialize()
    {
        this.testSubject = new EnvironmentVariableService();
        Environment.SetEnvironmentVariable(MyEnvVar, "true");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Environment.SetEnvironmentVariable(MyEnvVar, null);
    }

    [TestMethod]
    public void DoesEnvironmentVariableExist_ChecksAreCaseInsensitive()
    {
        this.testSubject.DoesEnvironmentVariableExist("THIS_ENVIRONMENT_VARIABLE_DOES_NOT_EXIST").Should().BeFalse();

        this.testSubject.DoesEnvironmentVariableExist(MyEnvVar).Should().BeTrue();
        this.testSubject.DoesEnvironmentVariableExist(MyEnvVar.ToLower()).Should().BeTrue();
        this.testSubject.DoesEnvironmentVariableExist(MyEnvVar.ToUpper()).Should().BeTrue();
    }

    [TestMethod]
    public void GetEnvironmentVariable_returnNullIfVariableDoesNotExist()
    {
        this.testSubject.GetEnvironmentVariable("NonExistentVar").Should().BeNull();
    }

    [TestMethod]
    public void GetEnvironmentVariable_returnCorrectValue()
    {
        string envVariableKey = nameof(envVariableKey);
        string envVariableValue = nameof(envVariableValue);
        Environment.SetEnvironmentVariable(envVariableKey, envVariableValue);
        var result = this.testSubject.GetEnvironmentVariable(envVariableKey);
        result.Should().NotBeNull();
        envVariableValue.Should().Be(result);
        Environment.SetEnvironmentVariable(envVariableKey, null);
    }

    [TestMethod]
    public void IsEnvironmentVariableValueTrue_returnsTrueForValidKey_caseInsensitive()
    {
        string envVariableKey1 = nameof(envVariableKey1);
        string envVariableKey2 = nameof(envVariableKey2);
        Environment.SetEnvironmentVariable(envVariableKey1, "True");
        Environment.SetEnvironmentVariable(envVariableKey2, "tRuE");
        var result1 = this.testSubject.IsEnvironmentVariableValueTrue(envVariableKey1);
        var result2 = this.testSubject.IsEnvironmentVariableValueTrue(envVariableKey1);
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        Environment.SetEnvironmentVariable(envVariableKey1, null);
        Environment.SetEnvironmentVariable(envVariableKey2, null);
    }

    [TestMethod]
    public void IsEnvironmentVariableValueTrue_returnsFalseForValidKey_caseInsensitive()
    {
        string envVariableKey1 = nameof(envVariableKey1);
        string envVariableKey2 = nameof(envVariableKey2);
        Environment.SetEnvironmentVariable(envVariableKey1, "False");
        Environment.SetEnvironmentVariable(envVariableKey2, "fAlSe");
        var result1 = this.testSubject.IsEnvironmentVariableValueTrue(envVariableKey1);
        var result2 = this.testSubject.IsEnvironmentVariableValueTrue(envVariableKey1);
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        Environment.SetEnvironmentVariable(envVariableKey1, null);
        Environment.SetEnvironmentVariable(envVariableKey2, null);
    }

    [TestMethod]
    public void IsEnvironmentVariableValueTrue_returnsFalseForInvalidAndNull()
    {
        string envVariableKey1 = nameof(envVariableKey1);
        string nonExistentKey = nameof(nonExistentKey);
        Environment.SetEnvironmentVariable(envVariableKey1, "notABoolean");
        var result1 = this.testSubject.IsEnvironmentVariableValueTrue(envVariableKey1);
        var result2 = this.testSubject.IsEnvironmentVariableValueTrue(nonExistentKey);
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        Environment.SetEnvironmentVariable(envVariableKey1, null);
    }

    [TestMethod]
    public void GetListEnvironmentVariable_returnEmptyIfVariableDoesNotExist()
    {
        this.testSubject.GetListEnvironmentVariable("NonExistentVar", ",").Should().BeEmpty();
    }

    [TestMethod]
    public void GetListEnvironmentVariable_emptyListIfEmptyVar()
    {
        var key = "foo";
        Environment.SetEnvironmentVariable(key, string.Empty);
        var result = this.testSubject.GetListEnvironmentVariable(key, ",");
        result.Should().NotBeNull();
        result.Should().BeEmpty();
        Environment.SetEnvironmentVariable(key, null);
    }

    [TestMethod]
    public void GetListEnvironmentVariable_singleItem()
    {
        var key = "foo";
        Environment.SetEnvironmentVariable(key, "bar");
        var result = this.testSubject.GetListEnvironmentVariable(key, ",");
        result.Should().ContainSingle();
        result.Should().Contain("bar");
        Environment.SetEnvironmentVariable(key, null);
    }

    [TestMethod]
    public void GetListEnvironmentVariable_multipleItems()
    {
        var key = "foo";
        Environment.SetEnvironmentVariable(key, "bar,baz,qux");
        var result = this.testSubject.GetListEnvironmentVariable(key, ",");
        result.Should().HaveCount(3);
        result.Should().Contain("bar");
        result.Should().Contain("baz");
        result.Should().Contain("qux");
        Environment.SetEnvironmentVariable(key, null);
    }
}

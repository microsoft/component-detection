namespace Microsoft.ComponentDetection.Orchestrator.Tests.Extensions;

using System;
using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CommaDelimitedConverterTests
{
    private readonly CommaDelimitedConverter converter;

    public CommaDelimitedConverterTests() => this.converter = new CommaDelimitedConverter();

    [TestMethod]
    public void ConvertFrom_WithNullValue_ThrowsNotSupportedException()
    {
        var act = () => this.converter.ConvertFrom(null);
        act.Should().ThrowExactly<NotSupportedException>();
    }

    [TestMethod]
    public void ConvertFrom_WithEmptyValue_ReturnsEmptyArray()
    {
        var result = this.converter.ConvertFrom(string.Empty);
        result.Should().BeEquivalentTo(Array.Empty<string>());
    }

    [TestMethod]
    public void ConvertFrom_WithSingleValue_ReturnsArrayWithSingleValue()
    {
        var result = this.converter.ConvertFrom("foo");
        result.Should().BeEquivalentTo(new[] { "foo" });
    }

    [TestMethod]
    public void ConvertFrom_WithMultipleValues_ReturnsArrayWithMultipleValues()
    {
        var result = this.converter.ConvertFrom("foo,bar");
        result.Should().BeEquivalentTo(new[] { "foo", "bar" });
    }

    [TestMethod]
    public void ConvertFrom_WithMultipleValuesAndSpaces_ReturnsArrayWithMultipleValues()
    {
        var result = this.converter.ConvertFrom("foo, bar");
        result.Should().BeEquivalentTo(new[] { "foo", "bar" });
    }

    [TestMethod]
    public void ConvertFrom_WithMultipleValuesAndSpacesAndEmpty_ReturnsArrayWithMultipleValues()
    {
        var result = this.converter.ConvertFrom("foo, ,bar");
        result.Should().BeEquivalentTo(new[] { "foo", "bar" });
    }
}

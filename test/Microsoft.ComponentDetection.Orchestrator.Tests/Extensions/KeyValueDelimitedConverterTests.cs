namespace Microsoft.ComponentDetection.Orchestrator.Tests.Extensions;

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class KeyValueDelimitedConverterTests
{
    private readonly KeyValueDelimitedConverter converter;

    public KeyValueDelimitedConverterTests() => this.converter = new KeyValueDelimitedConverter();

    [TestMethod]
    public void ConvertFrom_WithNullValue_ThrowsNotSupportedException()
    {
        var act = () => this.converter.ConvertFrom(null);
        act.Should().ThrowExactly<NotSupportedException>();
    }

    [TestMethod]
    public void ConvertFrom_WithEmptyValue_ReturnsEmptyDictionary()
    {
        var result = this.converter.ConvertFrom(string.Empty);
        result.Should().BeEquivalentTo(new Dictionary<string, string>());
    }

    [TestMethod]
    public void ConvertFrom_WithSingleValue_ReturnsDictionaryWithSingleValue()
    {
        var result = this.converter.ConvertFrom("foo=bar");
        result.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                {
                    "foo", "bar"
                },
            });
    }

    [TestMethod]
    public void ConvertFrom_WithMultipleValues_ReturnsDictionaryWithMultipleValues()
    {
        var result = this.converter.ConvertFrom("foo=bar,baz=qux");
        result.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                {
                    "foo", "bar"
                },
                {
                    "baz", "qux"
                },
            });
    }

    [TestMethod]
    public void ConvertFrom_WithMultipleValuesAndSpaces_ReturnsDictionaryWithMultipleValues()
    {
        var result = this.converter.ConvertFrom("foo=bar, baz=qux");
        result.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                {
                    "foo", "bar"
                },
                {
                    "baz", "qux"
                },
            });
    }

    [TestMethod]
    public void ConvertFrom_WithMultipleValuesAndSpacesAndEmpty_ReturnsDictionaryWithMultipleValues()
    {
        var result = this.converter.ConvertFrom("foo=bar, ,baz=qux");
        result.Should().BeEquivalentTo(
            new Dictionary<string, string>
            {
                {
                    "foo", "bar"
                },
                {
                    "baz", "qux"
                },
            });
    }

    [TestMethod]
    public void ConvertFrom_WithSingleKeyMissingValue_ThrowsFormatException()
    {
        var act = () => this.converter.ConvertFrom("foo=");
        act.Should().ThrowExactly<FormatException>().WithMessage("Invalid key value pair: foo=");
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PatternMatchingUtilityTests
{
    [TestMethod]
    [DataRow("test*", "test123", true)]
    [DataRow("test*", "123test", false)]
    [DataRow("*test", "123test", true)]
    [DataRow("*test", "test123", false)]
    [DataRow("test", "test", true)]
    [DataRow("test", "TEST", true)]
    [DataRow("test", "123test", false)]
    [DataRow("*test*", "123test456", true)]
    [DataRow("*test*", "test456", true)]
    [DataRow("*test*", "123test", true)]
    [DataRow("*test*", "test", true)]
    [DataRow("*test*", "tes", false)]
    [DataRow("*", "anything", true)]
    [DataRow("*", "", false)]
    [DataRow("**", "anything", true)]
    [DataRow("**", "", false)]
    [DataRow("*.csproj", "MyProject.csproj", true)]
    [DataRow("*.csproj", "MyProject.json", false)]
    [DataRow("package.json", "package.json", true)]
    [DataRow("package.json", "PACKAGE.JSON", true)]
    [DataRow("dockerfile.*", "dockerfile.prod", true)]
    [DataRow("dockerfile.*", "dockerfile", false)]
    [DataRow("*.cargo-sbom.json", "test.cargo-sbom.json", true)]
    [DataRow("*values*.yaml", "myvalues.yaml", true)]
    [DataRow("*values*.yaml", "values.yaml", true)]
    [DataRow("*values*.yaml", "values.json", false)]
    public void PatternMatcher_MatchesExpected(string pattern, string input, bool expected)
    {
        var matcher = PatternMatchingUtility.Compile([pattern]);

        matcher.IsMatch(input.AsSpan()).Should().Be(expected);
    }

    [TestMethod]
    public void PatternMatcher_MultiplePatterns_MatchesAny()
    {
        var matcher = PatternMatchingUtility.Compile(["a*", "*b"]);

        matcher.IsMatch("apple".AsSpan()).Should().BeTrue();
        matcher.IsMatch("crab".AsSpan()).Should().BeTrue();
        matcher.IsMatch("middle".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void PatternMatcher_EmptyPatterns_DoesNotThrow()
    {
        var matcher = PatternMatchingUtility.Compile([]);

        matcher.IsMatch("anything".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void MatchesPattern_SinglePattern()
    {
        PatternMatchingUtility.MatchesPattern("*.json", "package.json").Should().BeTrue();
        PatternMatchingUtility.MatchesPattern("*.json", "package.yaml").Should().BeFalse();
    }

    [TestMethod]
    public void MatchesPattern_CaseInsensitive()
    {
        PatternMatchingUtility.MatchesPattern("package.json", "PACKAGE.JSON").Should().BeTrue();
    }

    [TestMethod]
    public void GetMatchingPattern_ReturnsFirstMatch()
    {
        var result = PatternMatchingUtility.GetMatchingPattern("package.json", ["*.json", "package.json"]);
        result.Should().Be("*.json");
    }

    [TestMethod]
    public void GetMatchingPattern_ReturnsNullWhenNoMatch()
    {
        var result = PatternMatchingUtility.GetMatchingPattern("package.yaml", ["*.json", "*.xml"]);
        result.Should().BeNull();
    }

    [TestMethod]
    public void CompiledMatcher_GetMatchingPattern_Works()
    {
        var compiled = PatternMatchingUtility.Compile(["*.json", "*.xml", "Cargo.toml"]);

        compiled.GetMatchingPattern("package.json").Should().Be("*.json");
        compiled.GetMatchingPattern("pom.xml").Should().Be("*.xml");
        compiled.GetMatchingPattern("Cargo.toml").Should().Be("Cargo.toml");
        compiled.GetMatchingPattern("README.md").Should().BeNull();
    }

    [TestMethod]
    public void CompiledMatcher_IsMatch_SpanBased()
    {
        var compiled = PatternMatchingUtility.Compile(["package.json", "*.lock"]);

        compiled.IsMatch("package.json".AsSpan()).Should().BeTrue();
        compiled.IsMatch("yarn.lock".AsSpan()).Should().BeTrue();
        compiled.IsMatch("README.md".AsSpan()).Should().BeFalse();
    }
}

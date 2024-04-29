namespace Microsoft.ComponentDetection.Orchestrator.Tests;

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MinimatchTests
{
    [TestMethod]
    public void Equality_Matches()
    {
        this.AssertMinimatchOuter(
            "abc", // pattern
            new[] { "abc" }, // matches
            new[] { "ab", "abd", "abcd" }); // does not match
    }

    [TestMethod]
    public void SingleStar_MatchesWithinSegment()
    {
        this.AssertMinimatchOuter(
            "xxx.*", // pattern
            new[] { "xxx.yyy", "xxx.xxx" }, // matches
            new[] { "abcxxx.yyy", "xxx.y/z" }); // does not match
    }

    [TestMethod]
    public void SingleStar_MatchesWholeSegment()
    {
        this.AssertMinimatchOuter(
            "xxx/*/yyy", // pattern
            new[] { "xxx/abc/yyy" }, // matches
            new[] { "xxx/yyy", "xxx/abc/def/yyy", "xxx/.abc/yyy" }); // does not match
    }

    [TestMethod]
    public void DoubleStar_Matches_AnyDepth()
    {
        this.AssertMinimatchOuter(
            "xxx/**/yyy", // pattern
            new[] { "xxx/abc/yyy", "xxx/yyy", "xxx/abc/def/yyy" }, // matches
            new[] { "xxx/.abc/yyy" }); // does not match
    }

    [TestMethod]
    public void DoubleStar_Matches_Something()
    {
        this.AssertMinimatchOuter(
            "xxx/**yyy", // pattern
            new[] { "xxx/yyy" }, // matches
            new[] { "xxx/abc/yyy", "xxx/abc/def/yyy", "xxx/.abc/yyy" }); // does not match
    }

    [TestMethod]
    public void Questionmark_Matches_SingleCharacter()
    {
        this.AssertMinimatchOuter(
            "x?y", // pattern
            new[] { "xAy" }, // matches
            new[] { "xy", "xABy", "x/y" }); // does not match
    }

    [TestMethod]
    public void Braces_Expands()
    {
        this.AssertMinimatchOuter(
            "{foo,bar}", // pattern
            new[] { "foo", "bar" }, // matches
            new[] { "baz" }); // does not match
    }

    [TestMethod]
    public void Braces_Expansion_IncludesStars()
    {
        this.AssertMinimatchOuter(
            "{x,y/*}/z", // pattern
            new[] { "x/z", "y/a/z" }, // matches
            new[] { "y/z" }); // does not match
    }

    [TestMethod]
    public void Braces_Expands_Ranges()
    {
        this.AssertMinimatchOuter(
            "foo{1..3}", // pattern
            new[] { "foo1", "foo2", "foo3" }, // matches
            new[] { "foo", "foo0" }); // does not match
    }

    [TestMethod]
    public void ExclamationMark_Negates_Result()
    {
        this.AssertMinimatchOuter(
            "!abc", // pattern
            new[] { "a", "xyz" }, // matches
            new[] { "abc" }); // does not match
    }

    [TestMethod]
    public void HashTag_Is_Comment()
    {
        this.AssertMinimatchOuter(
            "#abc", // pattern
            Array.Empty<string>(), // matches
            new[] { "abc", "#abc" }); // does not match
    }

    [TestMethod]
    public void ExtGlobPlus_Matches_OneOrMore()
    {
        this.AssertMinimatchOuter(
            "a+(xy)", // pattern
            new[] { "axy", "axyxy" }, // matches
            new[] { "a" }); // does not match
    }

    [TestMethod]
    public void ExtGlobStar_Matches_ZeroOrMore()
    {
        this.AssertMinimatchOuter(
            "a*(xy)", // pattern
            new[] { "a", "axy", "axyxy" }, // matches
            new[] { "xy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobQuestionMark_Matches_ZeroOrOne()
    {
        this.AssertMinimatchOuter(
            "a?(xy)", // pattern
            new[] { "a", "axy" }, // matches
            new[] { "axyxy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobAt_Matches_One()
    {
        this.AssertMinimatchOuter(
            "a@(xy)", // pattern
            new[] { "axy" }, // matches
            new[] { "a", "axyxy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobExclamationMark_Negates_Pattern()
    {
        this.AssertMinimatchOuter(
            "a!(xy)", // pattern
            new[] { "ax" }, // matches
            new[] { "axy", "axyz" }); // does not match
    }

    [TestMethod]
    public void CaseSensitive()
    {
        this.AssertMinimatchOuter(
            "AbC", // pattern
            new[] { "AbC" }, // matches
            new[] { "ABC", "abc" }); // does not match
    }

    [TestMethod]
    public void CaseInsensitive()
    {
        this.AssertMinimatchOuter(
            "AbC", // pattern
            new[] { "AbC", "ABC", "abc" }, // matches
            new[] { "Ab" }, // does not match
            true);
    }

    private void AssertMinimatchOuter(string pattern, string[] matches, string[] mismatches, bool ignoreCase = false)
    {
        this.AssertMinimatch(pattern, matches, mismatches, true, ignoreCase);
        this.AssertMinimatch(pattern, matches, mismatches, false, ignoreCase);
    }

    private void AssertMinimatch(string pattern, string[] matches, string[] mismatches, bool isWindows, bool ignoreCase)
    {
        var divider = isWindows ? "\\" : "/";
        var minimatch = new Minimatch(pattern.Replace("/", divider), ignoreCase, isWindows);

        foreach (var match in matches.Select(x => x.Replace("/", divider)))
        {
            minimatch.IsMatch(match).Should().BeTrue($"'{pattern}' == '{match}'");
        }

        foreach (var mismatch in mismatches.Select(x => x.Replace("/", divider)))
        {
            minimatch.IsMatch(mismatch).Should().BeFalse($"'{pattern}' != '{mismatch}'");
        }
    }
}

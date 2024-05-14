namespace Microsoft.ComponentDetection.Orchestrator.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        AssertMinimatchBoth(
            "abc", // pattern
            new[] { "abc" }, // matches
            new[] { "ab", "abd", "abcd" }); // does not match
    }

    [TestMethod]
    public void SingleStar_MatchesWithinSegment()
    {
        AssertMinimatchBoth(
            "xxx.*", // pattern
            new[] { "xxx.yyy", "xxx.xxx" }, // matches
            new[] { "abcxxx.yyy", "xxx.y/z" }); // does not match
    }

    [TestMethod]
    public void SingleStar_MatchesWholeSegment()
    {
        AssertMinimatchBoth(
            "xxx/*/yyy", // pattern
            new[] { "xxx/abc/yyy" }, // matches
            new[] { "xxx/yyy", "xxx/abc/def/yyy", "xxx/.abc/yyy", "xxx/./yyy", "xxx/../yyy" }); // does not match
    }

    [TestMethod]
    public void DoubleStarSegment_Matches_AnyDepth()
    {
        AssertMinimatchBoth(
            "xxx/**/yyy", // pattern
            new[] { "xxx/yyy", "xxx/abc/yyy", "xxx/abc/def/yyy" }, // matches
            new[] { "xxx/.abc/yyy", "xxx/./yyy", "xxx/../yyy" }); // does not match
    }

    [TestMethod]
    public void TrailingDoubleStarSegment_Matches_AnyDepth()
    {
        AssertMinimatchBoth(
            "xxx/**", // pattern
            new[] { "xxx/yyy", "xxx/abc/yyy", "xxx/abc/def/yyy" }, // matches
            new[] { "yyy/xxx", "xxx/.abc/yyy", "xxx/./yyy", "xxx/../yyy" }); // does not match
    }

    [TestMethod]
    public void DoubleStar_Matches_WithinSegment()
    {
        AssertMinimatchBoth(
            "xxx/**yyy", // pattern
            new[] { "xxx/yyy", "xxx/ayyy" }, // matches
            new[] { "xxx/abc/yyy", "xxx/abc/def/yyy", "xxx/.abc/yyy" }); // does not match
    }

    [TestMethod]
    public void QuestionMark_Matches_SingleCharacter()
    {
        AssertMinimatchBoth(
            "x?y", // pattern
            new[] { "xAy" }, // matches
            new[] { "xy", "xABy", "x/y" }); // does not match
    }

    [TestMethod]
    public void Braces_Expands()
    {
        AssertMinimatchBoth(
            "{foo,bar}", // pattern
            new[] { "foo", "bar" }, // matches
            new[] { "baz" }); // does not match
    }

    [TestMethod]
    public void Braces_Expansion_IncludesStars()
    {
        AssertMinimatchBoth(
            "{x,y/*}/z", // pattern
            new[] { "x/z", "y/a/z" }, // matches
            new[] { "y/z" }); // does not match
    }

    [TestMethod]
    public void Braces_Expands_Ranges()
    {
        AssertMinimatchBoth(
            "foo{1..3}", // pattern
            new[] { "foo1", "foo2", "foo3" }, // matches
            new[] { "foo", "foo0" }); // does not match
    }

    [TestMethod]
    public void Braces_Expands_Ranges_Complex()
    {
        AssertMinimatchBoth(
            "a{b,c{d,e},{f,g}h}x{y,z}", // pattern
            new[] { "abxy", "abxz", "acdxy", "acdxz", "acexy", "acexz", "afhxy", "afhxz", "aghxy", "aghxz" }, // matches
            Array.Empty<string>()); // does not match
    }

    [TestMethod]
    public void Braces_ConsideredLiteral_IfNotClosed()
    {
        AssertMinimatchBoth(
            "a,b}{c,d", // pattern
            new[] { "a,b}{c,d" }, // matches
            new[] { "ac", "ad", "bc", "bd" }); // does not match
    }

    [TestMethod]
    public void ExclamationMark_Negates_Result()
    {
        AssertMinimatchBoth(
            "!abc", // pattern
            new[] { "a", "xyz" }, // matches
            new[] { "abc" }); // does not match
    }

    [TestMethod]
    public void ExclamationMark_NegatesGroup_Result()
    {
        // new[] { "asd.jss.xyz", "asd.sjs.zxy", "asd..xyz" }, // matches
        AssertMinimatchBoth(
            "*.!(js).!(xy)", // pattern
            new[] { "asd.sjs.zxy" }, // matches
            new[] { "asd.jss.xy", "asd.js.xyz", "asd.js.xy", "asd..xy" }); // does not match
    }

    [TestMethod]
    public void HashTag_Is_Comment()
    {
        AssertMinimatchBoth(
            "#abc", // pattern
            Array.Empty<string>(), // matches
            new[] { "abc", "#abc" }); // does not match
    }

    [TestMethod]
    public void ParanthesisWithoutStateChar_ConsideredLiteral()
    {
        AssertMinimatchBoth(
            "a(xy)", // pattern
            new[] { "a(xy)" }, // matches
            new[] { "axy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobPlus_Matches_OneOrMore()
    {
        AssertMinimatchBoth(
            "a+(xy)", // pattern
            new[] { "axy", "axyxy" }, // matches
            new[] { "a" }); // does not match
    }

    [TestMethod]
    public void ExtGlobStar_Matches_ZeroOrMore()
    {
        AssertMinimatchBoth(
            "a*(xy)", // pattern
            new[] { "a", "axy", "axyxy" }, // matches
            new[] { "xy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobQuestionMark_Matches_ZeroOrOne()
    {
        AssertMinimatchBoth(
            "a?(xy)", // pattern
            new[] { "a", "axy" }, // matches
            new[] { "axyxy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobAt_Matches_One()
    {
        AssertMinimatchBoth(
            "a@(xy)", // pattern
            new[] { "axy" }, // matches
            new[] { "a", "axyxy" }); // does not match
    }

    [TestMethod]
    public void ExtGlobExclamationMark_Negates_Pattern()
    {
        AssertMinimatchBoth(
            "a!(xy)", // pattern
            new[] { "ax" }, // matches
            new[] { "axy", "axyz" }); // does not match
    }

    [TestMethod]
    public void ExtGlobExclamationMark_ConsideredLiteral_IfInside()
    {
        AssertMinimatchBoth(
            "@(!a)", // pattern
            new[] { "!a" }, // matches
            new[] { "a", "bc" }); // does not match
    }

    [TestMethod]
    public void ExtGlobPipe_Is_Or()
    {
        AssertMinimatchBoth(
            "a@(b|c)", // pattern
            new[] { "ab", "ac" }, // matches
            new[] { "abc" }); // does not match
    }

    [TestMethod]
    public void ExtGlob_Escaping()
    {
        AssertMinimatch(
            @"a@(d\|\\!e)", // pattern
            new[] { @"ad|\!e", @"ad|\!e" }, // matches
            new[] { @"ad|\\!e", @"ad", @"ad|\f" }, // does not match
            isWindows: false);
    }

    [TestMethod]
    public void ExtGlob_ConsideredLiteral_IfNotClosed()
    {
        AssertMinimatchBoth(
            "a@(b|c", // pattern
            new[] { "a@(b|c" }, // matches
            new[] { "ab", "ac" }); // does not match
    }

    [TestMethod]
    public void SquareBrackets_WorksLikeRegex()
    {
        AssertMinimatchBoth(
            @"[|c-dE-F]", // pattern
            new[] { "|", "c", "d", "E", "F" }, // matches
            new[] { "|c-dE-F", "cd", "C", "D", "e", "f" }); // does not match
    }

    [TestMethod]
    public void CaseSensitive()
    {
        AssertMinimatchBoth(
            "AbC", // pattern
            new[] { "AbC" }, // matches
            new[] { "ABC", "abc" }); // does not match
    }

    [TestMethod]
    public void Empty()
    {
        AssertMinimatchBoth(
            string.Empty, // pattern
            new[] { string.Empty }, // matches
            new[] { "A" }); // does not match
    }

    [TestMethod]
    public void EdgeCase1()
    {
        AssertMinimatch(
            @"\[b-a]*", // pattern
            new[] { "[b-a]x" }, // matches
            new[] { "a[]b", "a]b", "a[]]b", "a[[]b" }, // does not match
            isWindows: false);
    }

    [TestMethod]
    public void EdgeCase2()
    {
        AssertMinimatch(
            @"[b-a\]*", // pattern
            new[] { "[b-a]x" }, // matches
            new[] { "a[]b", "a]b", "a[]]b", "a[[]b" }, // does not match
            isWindows: false);
    }

    [TestMethod]
    public void EdgeCase3()
    {
        AssertMinimatchBoth(
            "a[]*", // pattern
            new[] { "a[]b", "a[]]b" }, // matches
            new[] { "[b-a]x", "a]b", "a[[]b" }); // does not match
    }

    [TestMethod]
    public void EdgeCase4()
    {
        AssertMinimatchBoth(
            "a[]]*", // pattern
            new[] { "a]b" }, // matches
            new[] { "a[]b", "[b-a]x", "a[]]b", "a[[]b" }); // does not match
    }

    [TestMethod]
    public void EdgeCase5()
    {
        AssertMinimatchBoth(
            "a[[]*", // pattern
            new[] { "a[]b", "a[]]b", "a[[]b" }, // matches
            new[] { "[b-a]x", "a]b" }); // does not match
    }

    [TestMethod]
    public void EdgeCase6()
    {
        AssertMinimatchBoth(
            "a[[]]*", // pattern
            new[] { "a[]b", "a[]]b" }, // matches
            new[] { "[b-a]x", "a]b", "a[[]b" }); // does not match
    }

    [TestMethod]
    public void PossibleToEscapeSpecialChars()
    {
        AssertMinimatch(
            @"\(\)\.\*\{\}\+\?\[\]\^\$\\\!\@\#", // pattern
            new[] { @"().*{}+?[]^$\!@#" }, // matches
            Array.Empty<string>(), // does not match
            isWindows: false);
    }

    [TestMethod]
    public void Comment_DoesntMatch()
    {
        AssertMinimatchBoth(
            "#abc", // pattern
            Array.Empty<string>(), // matches
            new[] { "#abc", "abc" }); // does not match
    }

    [TestMethod]
    public void CaseInsensitive()
    {
        AssertMinimatchBoth(
            "AbC", // pattern
            new[] { "AbC", "ABC", "abc" }, // matches
            new[] { "Ab" }, // does not match
            ignoreCase: true);
    }

    [TestMethod]
    public void NullPattern_Throws()
    {
        var shouldThrow = () => new Minimatch(null, true, true);
        _ = shouldThrow.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void ReverseRegexRange_Throws()
    {
        var minimatch = new Minimatch("[b-a]", true, true);
        var shouldThrow = () => minimatch.IsMatch("something");
        _ = shouldThrow.Should().ThrowExactly<RegexParseException>();
    }

    private static void AssertMinimatchBoth(
        string pattern,
        string[] matches,
        string[] mismatches,
        bool ignoreCase = false)
    {
        AssertMinimatch(pattern, matches, mismatches, false, ignoreCase);
        AssertMinimatch(pattern, matches, mismatches, true, ignoreCase);
    }

    private static void AssertMinimatch(
        string pattern,
        IEnumerable<string> matches,
        IEnumerable<string> mismatches,
        bool isWindows,
        bool ignoreCase = false)
    {
        var divider = isWindows ? @"\" : "/";
        var minimatch = new Minimatch(pattern.Replace("/", divider), ignoreCase, isWindows);

        foreach (var match in matches.Select(x => x.Replace("/", divider)))
        {
            _ = minimatch.IsMatch(match).Should().BeTrue($"'{pattern}' == '{match}'");
        }

        foreach (var mismatch in mismatches.Select(x => x.Replace("/", divider)))
        {
            _ = minimatch.IsMatch(mismatch).Should().BeFalse($"'{pattern}' != '{mismatch}'");
        }
    }
}

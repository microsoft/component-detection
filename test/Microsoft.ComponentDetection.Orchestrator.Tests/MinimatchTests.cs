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
            ["abc"], // matches
            ["ab", "abd", "abcd"]); // does not match
    }

    [TestMethod]
    public void SingleStar_MatchesWithinSegment()
    {
        AssertMinimatchBoth(
            "xxx.*", // pattern
            ["xxx.yyy", "xxx.xxx"], // matches
            ["abcxxx.yyy", "xxx.y/z"]); // does not match
    }

    [TestMethod]
    public void SingleStar_MatchesWholeSegment()
    {
        AssertMinimatchBoth(
            "xxx/*/yyy", // pattern
            ["xxx/abc/yyy"], // matches
            ["xxx/yyy", "xxx/abc/def/yyy", "xxx/.abc/yyy", "xxx/./yyy", "xxx/../yyy"]); // does not match
    }

    [TestMethod]
    public void DoubleStarSegment_Matches_AnyDepth()
    {
        AssertMinimatchBoth(
            "xxx/**/yyy", // pattern
            ["xxx/yyy", "xxx/abc/yyy", "xxx/abc/def/yyy"], // matches
            ["xxx/.abc/yyy", "xxx/./yyy", "xxx/../yyy"]); // does not match
    }

    [TestMethod]
    public void TrailingDoubleStarSegment_Matches_AnyDepth()
    {
        AssertMinimatchBoth(
            "xxx/**", // pattern
            ["xxx/yyy", "xxx/abc/yyy", "xxx/abc/def/yyy"], // matches
            ["yyy/xxx", "xxx/.abc/yyy", "xxx/./yyy", "xxx/../yyy"]); // does not match
    }

    [TestMethod]
    public void DoubleStar_Matches_WithinSegment()
    {
        AssertMinimatchBoth(
            "xxx/**yyy", // pattern
            ["xxx/yyy", "xxx/ayyy"], // matches
            ["xxx/abc/yyy", "xxx/abc/def/yyy", "xxx/.abc/yyy"]); // does not match
    }

    [TestMethod]
    public void QuestionMark_Matches_SingleCharacter()
    {
        AssertMinimatchBoth(
            "x?y", // pattern
            ["xAy"], // matches
            ["xy", "xABy", "x/y"]); // does not match
    }

    [TestMethod]
    public void Braces_Expands()
    {
        AssertMinimatchBoth(
            "{foo,bar}", // pattern
            ["foo", "bar"], // matches
            ["baz"]); // does not match
    }

    [TestMethod]
    public void Braces_Expansion_IncludesStars()
    {
        AssertMinimatchBoth(
            "{x,y/*}/z", // pattern
            ["x/z", "y/a/z"], // matches
            ["y/z"]); // does not match
    }

    [TestMethod]
    public void Braces_Expands_Ranges()
    {
        AssertMinimatchBoth(
            "foo{1..3}", // pattern
            ["foo1", "foo2", "foo3"], // matches
            ["foo", "foo0"]); // does not match
    }

    [TestMethod]
    public void Braces_Expands_Ranges_Complex()
    {
        AssertMinimatchBoth(
            "a{b,c{d,e},{f,g}h}x{y,z}", // pattern
            ["abxy", "abxz", "acdxy", "acdxz", "acexy", "acexz", "afhxy", "afhxz", "aghxy", "aghxz"], // matches
            []); // does not match
    }

    [TestMethod]
    public void Braces_ConsideredLiteral_IfNotClosed()
    {
        AssertMinimatchBoth(
            "a,b}{c,d", // pattern
            ["a,b}{c,d"], // matches
            ["ac", "ad", "bc", "bd"]); // does not match
    }

    [TestMethod]
    public void ExclamationMark_Negates_Result()
    {
        AssertMinimatchBoth(
            "!abc", // pattern
            ["a", "xyz"], // matches
            ["abc"]); // does not match
    }

    [TestMethod]
    public void ExclamationMark_NegatesGroup_Result()
    {
        // new[] { "asd.jss.xyz", "asd.sjs.zxy", "asd..xyz" }, // matches
        AssertMinimatchBoth(
            "*.!(js).!(xy)", // pattern
            ["asd.sjs.zxy"], // matches
            ["asd.jss.xy", "asd.js.xyz", "asd.js.xy", "asd..xy"]); // does not match
    }

    [TestMethod]
    public void HashTag_Is_Comment()
    {
        AssertMinimatchBoth(
            "#abc", // pattern
            [], // matches
            ["abc", "#abc"]); // does not match
    }

    [TestMethod]
    public void ParanthesisWithoutStateChar_ConsideredLiteral()
    {
        AssertMinimatchBoth(
            "a(xy)", // pattern
            ["a(xy)"], // matches
            ["axy"]); // does not match
    }

    [TestMethod]
    public void ExtGlobPlus_Matches_OneOrMore()
    {
        AssertMinimatchBoth(
            "a+(xy)", // pattern
            ["axy", "axyxy"], // matches
            ["a"]); // does not match
    }

    [TestMethod]
    public void ExtGlobStar_Matches_ZeroOrMore()
    {
        AssertMinimatchBoth(
            "a*(xy)", // pattern
            ["a", "axy", "axyxy"], // matches
            ["xy"]); // does not match
    }

    [TestMethod]
    public void ExtGlobQuestionMark_Matches_ZeroOrOne()
    {
        AssertMinimatchBoth(
            "a?(xy)", // pattern
            ["a", "axy"], // matches
            ["axyxy"]); // does not match
    }

    [TestMethod]
    public void ExtGlobAt_Matches_One()
    {
        AssertMinimatchBoth(
            "a@(xy)", // pattern
            ["axy"], // matches
            ["a", "axyxy"]); // does not match
    }

    [TestMethod]
    public void ExtGlobExclamationMark_Negates_Pattern()
    {
        AssertMinimatchBoth(
            "a!(xy)", // pattern
            ["ax"], // matches
            ["axy", "axyz"]); // does not match
    }

    [TestMethod]
    public void ExtGlobExclamationMark_ConsideredLiteral_IfInside()
    {
        AssertMinimatchBoth(
            "@(!a)", // pattern
            ["!a"], // matches
            ["a", "bc"]); // does not match
    }

    [TestMethod]
    public void ExtGlobPipe_Is_Or()
    {
        AssertMinimatchBoth(
            "a@(b|c)", // pattern
            ["ab", "ac"], // matches
            ["abc"]); // does not match
    }

    [TestMethod]
    public void ExtGlob_Escaping()
    {
        AssertMinimatch(
            @"a@(d\|\\!e)", // pattern
            [@"ad|\!e", @"ad|\!e"], // matches
            [@"ad|\\!e", @"ad", @"ad|\f"], // does not match
            isWindows: false);
    }

    [TestMethod]
    public void ExtGlob_ConsideredLiteral_IfNotClosed()
    {
        AssertMinimatchBoth(
            "a@(b|c", // pattern
            ["a@(b|c"], // matches
            ["ab", "ac"]); // does not match
    }

    [TestMethod]
    public void SquareBrackets_WorksLikeRegex()
    {
        AssertMinimatchBoth(
            @"[|c-dE-F]", // pattern
            ["|", "c", "d", "E", "F"], // matches
            ["|c-dE-F", "cd", "C", "D", "e", "f"]); // does not match
    }

    [TestMethod]
    public void CaseSensitive()
    {
        AssertMinimatchBoth(
            "AbC", // pattern
            ["AbC"], // matches
            ["ABC", "abc"]); // does not match
    }

    [TestMethod]
    public void Empty()
    {
        AssertMinimatchBoth(
            string.Empty, // pattern
            [string.Empty], // matches
            ["A"]); // does not match
    }

    [TestMethod]
    public void EdgeCase1()
    {
        AssertMinimatch(
            @"\[b-a]*", // pattern
            ["[b-a]x"], // matches
            ["a[]b", "a]b", "a[]]b", "a[[]b"], // does not match
            isWindows: false);
    }

    [TestMethod]
    public void EdgeCase2()
    {
        AssertMinimatch(
            @"[b-a\]*", // pattern
            ["[b-a]x"], // matches
            ["a[]b", "a]b", "a[]]b", "a[[]b"], // does not match
            isWindows: false);
    }

    [TestMethod]
    public void EdgeCase3()
    {
        AssertMinimatchBoth(
            "a[]*", // pattern
            ["a[]b", "a[]]b"], // matches
            ["[b-a]x", "a]b", "a[[]b"]); // does not match
    }

    [TestMethod]
    public void EdgeCase4()
    {
        AssertMinimatchBoth(
            "a[]]*", // pattern
            ["a]b"], // matches
            ["a[]b", "[b-a]x", "a[]]b", "a[[]b"]); // does not match
    }

    [TestMethod]
    public void EdgeCase5()
    {
        AssertMinimatchBoth(
            "a[[]*", // pattern
            ["a[]b", "a[]]b", "a[[]b"], // matches
            ["[b-a]x", "a]b"]); // does not match
    }

    [TestMethod]
    public void EdgeCase6()
    {
        AssertMinimatchBoth(
            "a[[]]*", // pattern
            ["a[]b", "a[]]b"], // matches
            ["[b-a]x", "a]b", "a[[]b"]); // does not match
    }

    [TestMethod]
    public void PossibleToEscapeSpecialChars()
    {
        AssertMinimatch(
            @"\(\)\.\*\{\}\+\?\[\]\^\$\\\!\@\#", // pattern
            [@"().*{}+?[]^$\!@#"], // matches
            [], // does not match
            isWindows: false);
    }

    [TestMethod]
    public void Comment_DoesntMatch()
    {
        AssertMinimatchBoth(
            "#abc", // pattern
            [], // matches
            ["#abc", "abc"]); // does not match
    }

    [TestMethod]
    public void CaseInsensitive()
    {
        AssertMinimatchBoth(
            "AbC", // pattern
            ["AbC", "ABC", "abc"], // matches
            ["Ab"], // does not match
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

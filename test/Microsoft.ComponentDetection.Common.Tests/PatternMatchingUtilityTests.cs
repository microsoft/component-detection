#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PatternMatchingUtilityTests
{
    [TestMethod]
    public void PatternMatcher_Matches_StartsWith()
    {
        var pattern = "test*";
        var input = "test123";

        var matcher = PatternMatchingUtility.GetFilePatternMatcher([pattern]);

        matcher(input).Should().BeTrue();
        matcher("123test").Should().BeFalse();
    }

    [TestMethod]
    public void PatternMatcher_Matches_EndsWith()
    {
        var pattern = "*test";
        var input = "123test";

        var matcher = PatternMatchingUtility.GetFilePatternMatcher([pattern]);

        matcher(input).Should().BeTrue();
        matcher("test123").Should().BeFalse();
    }

    [TestMethod]
    public void PatternMatcher_Matches_Exact()
    {
        var pattern = "test";
        var input = "test";

        var matcher = PatternMatchingUtility.GetFilePatternMatcher([pattern]);

        matcher(input).Should().BeTrue();
        matcher("123test").Should().BeFalse();
    }
}

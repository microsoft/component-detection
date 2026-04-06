#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

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
    [DataRow("test", "123test", false)]
    [DataRow("*test*", "123test456", true)]
    [DataRow("*test*", "test456", true)]
    [DataRow("*test*", "123test", true)]
    [DataRow("*test*", "test", true)]
    [DataRow("*test*", "tes", false)]
    [DataRow("*", "anything", true)]
    [DataRow("*", "", true)]
    [DataRow("**", "anything", true)]
    [DataRow("**", "", true)]
    public void PatternMatcher_MatchesExpected(string pattern, string input, bool expected)
    {
        var matcher = PatternMatchingUtility.GetFilePatternMatcher([pattern]);

        matcher(input).Should().Be(expected);
    }
}

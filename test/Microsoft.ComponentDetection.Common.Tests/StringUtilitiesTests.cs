namespace Microsoft.ComponentDetection.Common.Tests;

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
[TestCategory("Governance/Utilities")]
public class StringUtilitiesTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow(null, null)]
    [DataRow("  ", "  ")]
    [DataRow(" https:// ", " https:// ")]
    [DataRow("https://username:password@domain.me", "https://******@domain.me")]
    [DataRow("https://domain.me", "https://domain.me")]
    [DataRow("https://@domain.me", "https://@domain.me")]
    [DataRow(
        "install -r requirements.txt --dry-run --ignore-installed --quiet --report file.zvn --index-url https://user:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@someregistry.localhost.com",
        "install -r requirements.txt --dry-run --ignore-installed --quiet --report file.zvn --index-url https://******@someregistry.localhost.com")]
    public void RemoveSensitiveInformation_ReturnsAsExpected(string input, string expected)
    {
        var actual = StringUtilities.RemoveSensitiveInformation(input);
        actual.Should().Be(expected);
    }
}

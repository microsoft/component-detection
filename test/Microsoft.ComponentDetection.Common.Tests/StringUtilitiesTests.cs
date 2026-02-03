#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using AwesomeAssertions;
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
    [DataRow("https://username:password@domain.me", $"https://{StringUtilities.SensitivePlaceholder}@domain.me")]
    [DataRow("HTTPS://username:password@domain.me", $"HTTPS://{StringUtilities.SensitivePlaceholder}@domain.me")]
    [DataRow("https://domain.me", "https://domain.me")]
    [DataRow("https://@domain.me", "https://@domain.me")]
    [DataRow(
        "install -r requirements.txt --dry-run --ignore-installed --quiet --report file.zvn --index-url https://user:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@someregistry.localhost.com",
        $"install -r requirements.txt --dry-run --ignore-installed --quiet --report file.zvn --index-url https://{StringUtilities.SensitivePlaceholder}@someregistry.localhost.com")]
    [DataRow(
        "pip install --index-url https://token@registry1.com/simple --extra-index-url https://secret@registry2.com/simple",
        $"pip install --index-url https://{StringUtilities.SensitivePlaceholder}@registry1.com/simple --extra-index-url https://{StringUtilities.SensitivePlaceholder}@registry2.com/simple")]
    public void RemoveSensitiveInformation_ReturnsAsExpected(string input, string expected)
    {
        var actual = StringUtilities.RemoveSensitiveInformation(input);
        actual.Should().Be(expected);
    }
}

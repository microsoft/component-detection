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
    [DataRow(
        "https://storage.blob.core.windows.net/container/file.blob?sv=2021-06-08&se=2024-01-01&sig=abcdef123456",
        $"https://storage.blob.core.windows.net/container/file.blob?{StringUtilities.SensitivePlaceholder}")]
    [DataRow(
        "WARNING: Retrying after connection broken: /b-41bf548673924b7aa7e3a735c767e3b3/file.blob?sv=2021-06-08&st=2024-01-01&se=2024-01-02&sig=verylongsignaturevalue123456789",
        $"WARNING: Retrying after connection broken: /b-41bf548673924b7aa7e3a735c767e3b3/file.blob?{StringUtilities.SensitivePlaceholder}")]
    [DataRow(
        "Multiple SAS tokens: https://host1.blob.core.windows.net/path?sig=token1 and https://host2.blob.core.windows.net/path?sv=2021&sig=token2",
        $"Multiple SAS tokens: https://host1.blob.core.windows.net/path?{StringUtilities.SensitivePlaceholder} and https://host2.blob.core.windows.net/path?{StringUtilities.SensitivePlaceholder}")]
    public void RemoveSensitiveInformation_ReturnsAsExpected(string input, string expected)
    {
        var actual = StringUtilities.RemoveSensitiveInformation(input);
        actual.Should().Be(expected);
    }
}

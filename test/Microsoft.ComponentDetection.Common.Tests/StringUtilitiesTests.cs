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

    // Plain HTTP URLs - credentials ARE masked (same as HTTPS)
    [DataRow("http://username:password@domain.me", $"http://{StringUtilities.SensitivePlaceholder}@domain.me")]
    [DataRow("http://domain.me?sig=token", $"http://domain.me?{StringUtilities.SensitivePlaceholder}")]

    // Mixed scenario: URL credentials AND SAS token in same string
    [DataRow(
        "https://user:pass@storage.blob.core.windows.net/container/file.blob?sv=2021-06-08&sig=abcdef123456",
        $"https://{StringUtilities.SensitivePlaceholder}@storage.blob.core.windows.net/container/file.blob?{StringUtilities.SensitivePlaceholder}")]
    [DataRow(
        "https://user%40email.com:p%40ssword@domain.me/path",
        $"https://{StringUtilities.SensitivePlaceholder}@domain.me/path")]
    [DataRow(
        "https://user:pass%3Dword@domain.me/path?query=value",
        $"https://{StringUtilities.SensitivePlaceholder}@domain.me/path?query=value")]
    [DataRow(
        "https://storage.blob.core.windows.net/path?sv=2021&sig=abc%2Fdef%3D123",
        $"https://storage.blob.core.windows.net/path?{StringUtilities.SensitivePlaceholder}")]

    // Edge case: @ symbol in query string - the regex will see this as credentials (known limitation)
    [DataRow("https://domain.me/path?email=user@example.com", $"https://{StringUtilities.SensitivePlaceholder}@example.com")]

    // Edge case: multiple @ symbols - non-greedy regex matches only up to first @
    [DataRow(
        "https://user@org:token@domain.me/path",
        $"https://{StringUtilities.SensitivePlaceholder}@org:token@domain.me/path")]

    // Edge case: SAS token at end of string with no trailing content
    [DataRow(
        "blob url: https://account.blob.core.windows.net/container/blob?sig=xyz",
        $"blob url: https://account.blob.core.windows.net/container/blob?{StringUtilities.SensitivePlaceholder}")]

    // Edge case: query string without sig parameter should NOT be masked
    [DataRow(
        "https://example.com/path?param1=value1&param2=value2",
        "https://example.com/path?param1=value1&param2=value2")]
    public void RemoveSensitiveInformation_ReturnsAsExpected(string input, string expected)
    {
        var actual = StringUtilities.RemoveSensitiveInformation(input);
        actual.Should().Be(expected);
    }
}

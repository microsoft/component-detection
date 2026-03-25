#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Text.RegularExpressions;

public static class StringUtilities
{
    // Matches credentials in URLs like http://user:password@host or https://user:password@host
    private static readonly Regex UrlCredentialsRegex = new(@"(?<=https?://)(.+?)(?=@)", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    // Matches SAS tokens in query strings (e.g., ?sv=...&sig=... or ?se=...&sig=...)
    // SAS tokens contain a 'sig' parameter which is the signature - we mask the entire query string
    private static readonly Regex SasTokenRegex = new(@"\?[^""'\s]*sig=[^""'\s]*", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

    public const string SensitivePlaceholder = "******";

    /// <summary>
    /// Utility method to remove sensitive information from a string, including:
    /// - Credentials in URLs (e.g., https://user:password@host)
    /// - SAS tokens in query strings (e.g., ?sv=...&amp;sig=...).
    /// </summary>
    /// <param name="inputString">String with possible credentials.</param>
    /// <returns>New string identical to original string, except sensitive information is replaced with placeholders.</returns>
    public static string RemoveSensitiveInformation(this string inputString)
    {
        if (string.IsNullOrWhiteSpace(inputString))
        {
            return inputString;
        }

        try
        {
            var result = UrlCredentialsRegex.Replace(inputString, SensitivePlaceholder);
            result = SasTokenRegex.Replace(result, $"?{SensitivePlaceholder}");
            return result;
        }
        catch (Exception)
        {
            // No matter the exception, we should not break flow due to regex failure/timeout.
            return inputString;
        }
    }
}

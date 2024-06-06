namespace Microsoft.ComponentDetection.Common;

using System;
using System.Text.RegularExpressions;

public static class StringUtilities
{
    private static readonly Regex SensitiveInfoRegex = new Regex(@"(?<=https://)(.+)(?=@)", RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Utility method to remove sensitive information from a string, currently focused on removing on the credentials placed within URL which can be part of CLI commands.
    /// </summary>
    /// <param name="inputString">String with possible credentials.</param>
    /// <returns>New string identical to original string, except credentials in URL are replaced with placeholders.</returns>
    public static string RemoveSensitiveInformation(this string inputString)
    {
        if (string.IsNullOrWhiteSpace(inputString))
        {
            return inputString;
        }

        try
        {
            return SensitiveInfoRegex.Replace(inputString, "******");
        }
        catch (Exception)
        {
            // No matter the exception, we should not break flow due to regex failure/timeout.
            return inputString;
        }
    }
}

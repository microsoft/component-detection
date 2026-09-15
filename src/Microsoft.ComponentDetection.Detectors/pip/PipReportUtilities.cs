#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Linq;
using System.Text.RegularExpressions;

internal class PipReportUtilities
{
    private const int MaxLicenseFieldLength = 100;
    private const string ClassifierFieldSeparator = " :: ";
    private const string ClassifierFieldLicensePrefix = "License";

    // Python regular expression for version schema:
    // https://www.python.org/dev/peps/pep-0440/#appendix-b-parsing-version-strings-with-regular-expressions
    public static readonly Regex CanonicalVersionPatternMatch = new Regex(
        @"^([1-9]\d*!)?(0|[1-9]\d*)(\.(0|[1-9]\d*))*((a|b|rc)(0|[1-9]\d*))?(\.post(0|[1-9]\d*))?(\.dev(0|[1-9]\d*))?(\+(?:(?<local>[a-z0-9]+(?:[.][a-z0-9]+)*))?)?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Normalize the package name format to the standard Python Packaging format.
    /// See https://packaging.python.org/en/latest/specifications/name-normalization/#name-normalization.
    /// </summary>
    /// <returns>
    /// The name lowercased with all runs of the characters ., -, or _
    /// replaced with a single - character.
    /// </returns>
#pragma warning disable CA1308 // Format requires lowercase.
    public static string NormalizePackageNameFormat(string packageName) =>
        Regex.Replace(packageName, @"[-_.]+", "-").ToLowerInvariant();
#pragma warning restore CA1308

    public static string GetSupplierFromInstalledItem(PipInstallationReportItem component)
    {
        if (!string.IsNullOrWhiteSpace(component.Metadata?.Maintainer))
        {
            return component.Metadata.Maintainer;
        }

        if (!string.IsNullOrWhiteSpace(component.Metadata?.MaintainerEmail))
        {
            return component.Metadata.MaintainerEmail;
        }

        if (!string.IsNullOrWhiteSpace(component.Metadata?.Author))
        {
            return component.Metadata.Author;
        }

        if (!string.IsNullOrWhiteSpace(component.Metadata?.AuthorEmail))
        {
            return component.Metadata.AuthorEmail;
        }

        // If none of the fields are populated, return null.
        return null;
    }

    public static string GetLicenseFromInstalledItem(PipInstallationReportItem component)
    {
        // There are cases where the actual license text is found in the license field so we limit the length of this field to 100 characters.
        if (component.Metadata?.License is not null && component.Metadata?.License.Length < MaxLicenseFieldLength)
        {
            return component.Metadata.License;
        }

        if (component.Metadata?.Classifier is not null)
        {
            var licenseClassifiers = component.Metadata.Classifier.Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith(ClassifierFieldLicensePrefix));

            // Split the license classifiers by the " :: " and take the last part of the string
            licenseClassifiers = licenseClassifiers.Select(x => x.Split(ClassifierFieldSeparator).Last()).ToList();

            return string.Join(", ", licenseClassifiers);
        }

        return null;
    }

    public static bool IsCanonicalVersion(string version)
    {
        return CanonicalVersionPatternMatch.Match(version).Success;
    }
}

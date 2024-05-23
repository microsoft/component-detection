namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Linq;
using System.Text.RegularExpressions;

internal class PipReportUtilities
{
    private const int MaxLicenseFieldLength = 100;
    private const string ClassifierFieldSeparator = " :: ";
    private const string ClassifierFieldLicensePrefix = "License";

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
}

namespace Microsoft.ComponentDetection.Detectors.Pip;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Represents a package and a list of dependency specifications that the package must be.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class PipDependencySpecification
{
    // Extracts name and version from a Requires-Dist string that is found in a metadata file
    public static readonly Regex RequiresDistRegex = new Regex(
        @"Requires-Dist:\s*(?:(.*?)\s*\((.*?)\)|([^\s;]*))",
        RegexOptions.Compiled);

    /// <summary>
    /// These are packages that we don't want to evaluate in our graph as they are generally python builtins.
    /// </summary>
    private static readonly HashSet<string> PackagesToIgnore = new HashSet<string>
    {
        "-markerlib",
        "pip",
        "pip-tools",
        "pip-review",
        "pkg-resources",
        "setuptools",
        "wheel",
    };

    // Extracts abcd from a string like abcd==1.*,!=1.3
    private static readonly Regex PipNameExtractionRegex = new Regex(
        @"^.+?((?=<)|(?=>)|(?=>=)|(?=<=)|(?===)|(?=!=)|(?=~=)|(?====))",
        RegexOptions.Compiled);

    // Extracts ==1.*,!=1.3 from a string like abcd==1.*,!=1.3
    private static readonly Regex PipVersionExtractionRegex = new Regex(
        @"((?=<)|(?=>)|(?=>=)|(?=<=)|(?===)|(?=!=)|(?=~=)|(?====))(.*)",
        RegexOptions.Compiled);

    /// <summary>
    /// This constructor is used in test code.
    /// </summary>
    public PipDependencySpecification()
    {
    }

    /// <summary>
    /// Constructs a dependency specification from a string in one of two formats (Requires-Dist: a (==1.3)) OR a==1.3.
    /// </summary>
    /// <param name="packageString">The <see cref="string"/> to parse.</param>
    /// <param name="requiresDist">The package format.</param>
    public PipDependencySpecification(string packageString, bool requiresDist = false)
    {
        if (requiresDist)
        {
            var distMatch = RequiresDistRegex.Match(packageString);

            for (var i = 1; i < distMatch.Groups.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(distMatch.Groups[i].Value))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(this.Name))
                {
                    this.Name = distMatch.Groups[i].Value;
                }
                else
                {
                    this.DependencySpecifiers = distMatch.Groups[i].Value.Split(',');
                }
            }
        }
        else
        {
            var nameMatches = PipNameExtractionRegex.Match(packageString);
            var versionMatches = PipVersionExtractionRegex.Match(packageString);

            if (nameMatches.Captures.Count > 0)
            {
                this.Name = nameMatches.Captures[0].Value;
            }
            else
            {
                this.Name = packageString;
            }

            if (versionMatches.Captures.Count > 0)
            {
                this.DependencySpecifiers = versionMatches.Captures[0].Value.Split(',');
            }
        }

        this.DependencySpecifiers = this.DependencySpecifiers.Where(x => !x.Contains("python_version")).ToList();
    }

    /// <summary>
    /// Gets or sets the package <see cref="Name"/> (ex: pyyaml).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the set of dependency specifications that constrain the overall dependency request (ex: ==1.0, >=2.0).
    /// </summary>
    public IList<string> DependencySpecifiers { get; set; } = new List<string>();

    private string DebuggerDisplay => $"{this.Name} ({string.Join(';', this.DependencySpecifiers)})";

    /// <summary>
    /// Whether or not the package is safe to resolve based on the packagesToIgnore.
    /// </summary>
    /// <returns> True if the package is unsafe, otherwise false. </returns>
    public bool PackageIsUnsafe()
    {
        return PackagesToIgnore.Contains(this.Name);
    }
}

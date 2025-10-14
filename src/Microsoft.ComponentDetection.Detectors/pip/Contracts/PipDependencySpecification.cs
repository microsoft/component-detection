#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a package and a list of dependency specifications that the package must be.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class PipDependencySpecification
{
    // Extracts name and version from a Requires-Dist string that is found in a metadata file
    public static readonly Regex RequiresDistRegex = new Regex(
        @"Requires-Dist:\s*([^\s;\[<>=!~]+)(?:\[[^\]]+\])?(?:\s*\(([^)]+)\))?([^;]*)",
        RegexOptions.Compiled);

    // Extracts name and version from a Requires-Dist string that is found in a metadata file
    public static readonly Regex RequiresDistConditionalDependenciesMatch = new Regex(
        @"(?<=.*;.*\s*)(?:and |or )?(?:\S+)\s*(?:<=|>=|<|>|===|==|!=|~=)\s*(?:\S+)",
        RegexOptions.Compiled);

    /// <summary>
    /// These are packages that we don't want to evaluate in our graph as they are generally python builtins.
    /// </summary>
    public static readonly HashSet<string> PackagesToIgnore =
    [
        "-markerlib",
        "pip",
        "pip-tools",
        "pip-review",
        "pkg-resources",
        "setuptools",
        "wheel",
    ];

    // Extracts abcd from a string like abcd==1.*,!=1.3
    private static readonly Regex PipNameExtractionRegex = new Regex(
        @"^.+?((?=<)|(?=>)|(?=>=)|(?=<=)|(?===)|(?=!=)|(?=~=)|(?====)|(?=\[))",
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
        => this.Initialize(packageString, requiresDist);

    /// <summary>
    /// Constructs a dependency specification from a string in one of two formats (Requires-Dist: a (==1.3)) OR a==1.3.
    /// </summary>
    /// <param name="packageString">The <see cref="string"/> to parse.</param>
    /// <param name="requiresDist">The package format.</param>
    /// <param name="logger">The logger used for events realting to the pip dependency specification.</param>
    public PipDependencySpecification(ILogger logger, string packageString, bool requiresDist = false)
    {
        this.Logger = logger;
        this.Initialize(packageString, requiresDist);
    }

    /// <summary>
    /// Gets or sets the package <see cref="Name"/> (ex: pyyaml).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the set of dependency specifications that constrain the overall dependency request (ex: ==1.0, >=2.0).
    /// </summary>
    public IList<string> DependencySpecifiers { get; set; } = [];

    public IList<string> ConditionalDependencySpecifiers { get; set; } = [];

    private string DebuggerDisplay => $"{this.Name} ({string.Join(';', this.DependencySpecifiers)})";

    private ILogger Logger { get; set; }

    private void Initialize(string packageString, bool requiresDist)
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
                    this.Name = distMatch.Groups[i].Value.Trim();
                }
                else
                {
                    this.DependencySpecifiers = distMatch.Groups[i].Value.Split(',');
                }
            }

            var conditionalDependenciesMatches = RequiresDistConditionalDependenciesMatch.Matches(packageString);

            for (var i = 0; i < conditionalDependenciesMatches.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(conditionalDependenciesMatches[i].Value))
                {
                    this.ConditionalDependencySpecifiers.Add(conditionalDependenciesMatches[i].Value);
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

        this.DependencySpecifiers = this.DependencySpecifiers.Where(x => !x.Contains("python_version"))
            .Select(x => x.Trim())
            .ToList();
    }

    /// <summary>
    /// Whether or not the package is safe to resolve based on the packagesToIgnore.
    /// </summary>
    /// <returns> True if the package is unsafe, otherwise false. </returns>
    public bool PackageIsUnsafe()
    {
        return PackagesToIgnore.Contains(this.Name);
    }

    /// <summary>
    /// Whether or not the package is safe to resolve based on the packagesToIgnore.
    /// </summary>
    /// <returns> True if the package meets all conditions.</returns>
    public bool PackageConditionsMet(Dictionary<string, string> pythonEnvironmentVariables)
    {
        var conditionalRegex = new Regex(@"(and|or)?\s*(\S+)\s*(<=|>=|<|>|===|==|!=|~=)\s*['""]?([^'""]+)['""]?", RegexOptions.Compiled);
        var conditionsMet = true;
        foreach (var conditional in this.ConditionalDependencySpecifiers)
        {
            var conditionMet = true;
            var conditionalMatch = conditionalRegex.Match(conditional);
            var conditionalJoinOperator = conditionalMatch.Groups[1].Value;
            var conditionalVar = conditionalMatch.Groups[2].Value;
            var conditionalOperator = conditionalMatch.Groups[3].Value;
            var conditionalValue = conditionalMatch.Groups[4].Value;
            if (!pythonEnvironmentVariables.ContainsKey(conditionalVar) || string.IsNullOrEmpty(pythonEnvironmentVariables[conditionalVar]))
            {
                continue; // If the variable isn't in the environment, we can't evaluate it.
            }

            if (string.Equals(conditionalVar, "python_version", StringComparison.OrdinalIgnoreCase))
            {
                var pythonVersion = PythonVersion.Create(conditionalValue);
                if (pythonVersion.Valid)
                {
                    var conditionalSpec = $"{conditionalOperator}{conditionalValue}";
                    try
                    {
                        conditionMet = PythonVersionUtilities.VersionValidForSpec(pythonEnvironmentVariables[conditionalVar], [conditionalSpec]);
                    }
                    catch (ArgumentException ae)
                    {
                        conditionMet = false;
                        this.Logger?.LogDebug("Could not create pip dependency: {ErrorMessage}", ae.Message);
                    }
                }
                else
                {
                    conditionMet = pythonEnvironmentVariables[conditionalVar] == conditionalValue;
                }
            }
            else if (string.Equals(conditionalVar, "sys_platform", StringComparison.OrdinalIgnoreCase))
            {
                // if the platform is not windows or linux (empty string in env var), allow the package to be added. Otherwise, ensure it matches the python condition
                conditionMet = string.Equals(pythonEnvironmentVariables[conditionalVar], conditionalValue, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // we don't know how to handle cases besides python_version or sys_platform, so allow the package
                continue;
            }

            if (conditionalJoinOperator == "or")
            {
                conditionsMet = conditionsMet || conditionMet;
            }
            else if (conditionalJoinOperator == "and" || string.IsNullOrEmpty(conditionalJoinOperator))
            {
                conditionsMet = conditionsMet && conditionMet;
            }
        }

        return conditionsMet;
    }

    /// <summary>
    /// Iterates through the package versions that are explicitly stated, and returns
    /// the highest version that adheres to the version requirements.
    /// </summary>
    /// <example>
    /// DependencySpecifiers: (&gt;=1.2.3, !=1.2.4, &lt;2.0.0)
    /// Result: 1.2.3
    /// Explaination: Even through 2.0.0 and 1.2.4 are higher, they do not adhere to the dep specifier requirements.
    /// </example>
    /// <returns>Highest explicitly stated version.</returns>
    public string GetHighestExplicitPackageVersion()
    {
        var versions = this.DependencySpecifiers
            .Select(x => PythonVersionUtilities.ParseSpec(x).Version.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        try
        {
            var topVersion = versions
                .Where(x => PythonVersionUtilities.VersionValidForSpec(x, this.DependencySpecifiers))
                .Select(x => (Version: x, PythonVersion: PythonVersion.Create(x)))
                .Where(x => x.PythonVersion.Valid)
                .OrderByDescending(x => x.PythonVersion)
                .FirstOrDefault((null, null));

            return topVersion.Version;
        }
        catch (ArgumentException ae)
        {
            this.Logger?.LogDebug("Could not create pip dependency: {ErrorMessage}", ae.Message);

            return null;
        }
    }

    /// <summary>
    /// Common method that can be used to determine whether this package is a valid parent
    /// package of another package. Note that this logic is not perfect, it does not
    /// respect all of the environment identifiers, nor does it correctly handle extras (it ignores
    /// them).
    /// </summary>
    /// <param name="pythonEnvironmentVariables">List of environment variables used to evaluate the environmant conditions, such as OS this is executing on.</param>
    /// <returns>Whether or not this package is valid as a parent package.</returns>
    public bool IsValidParentPackage(Dictionary<string, string> pythonEnvironmentVariables) =>
        !this.PackageIsUnsafe()
        && this.PackageConditionsMet(pythonEnvironmentVariables)
        && !this.ConditionalDependencySpecifiers.Any(s => s.Contains("extra ==", StringComparison.OrdinalIgnoreCase));
}

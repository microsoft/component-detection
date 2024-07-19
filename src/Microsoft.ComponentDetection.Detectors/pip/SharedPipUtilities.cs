namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public static class SharedPipUtilities
{
    /// <summary>
    /// Converts a list of parsed packages to a list of PipDependencySpecifications. Also performs some validations,
    /// such as filtering out unsafe packages and checking if the package conditions are met.
    /// </summary>
    /// <param name="parsedPackages">List of packages and git components.</param>
    /// <param name="pythonEnvironmentVariables">Python environment specifiers.</param>
    /// <returns>Enumerable containing the converted, sanitized Pip dependency specs.</returns>
    public static IEnumerable<PipDependencySpecification> ParsedPackagesToPipDependencies(
        IList<(string PackageString, GitComponent Component)> parsedPackages,
        Dictionary<string, string> pythonEnvironmentVariables) =>
            parsedPackages.Where(tuple => tuple.PackageString != null)
                .Select(tuple => tuple.PackageString)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new PipDependencySpecification(x))
                .Where(x => !x.PackageIsUnsafe())
                .Where(x => x.PackageConditionsMet(pythonEnvironmentVariables));
}

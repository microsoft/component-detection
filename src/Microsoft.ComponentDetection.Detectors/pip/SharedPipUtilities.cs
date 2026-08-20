#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public static class SharedPipUtilities
{
    /// <summary>
    /// Converts a list of parsed packages to a list of PipDependencySpecifications. Also performs some validations,
    /// such as filtering out unsafe packages and checking if the package conditions are met.
    /// </summary>
    /// <param name="parsedPackages">List of packages and git components.</param>
    /// <param name="pythonEnvironmentVariables">Python environment specifiers.</param>
    /// <param name="logger">Logger for pip dependency specification.</param>
    /// <returns>Enumerable containing the converted, sanitized Pip dependency specs.</returns>
    public static IEnumerable<PipDependencySpecification> ParsedPackagesToPipDependencies(
        IList<(string PackageString, GitComponent Component)> parsedPackages,
        Dictionary<string, string> pythonEnvironmentVariables,
        ILogger logger) =>
            parsedPackages.Where(tuple => tuple.PackageString != null)
                .Select(tuple => tuple.PackageString)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new PipDependencySpecification(logger, x, false))
                .Where(x => !x.PackageIsUnsafe())
                .Where(x => x.PackageConditionsMet(pythonEnvironmentVariables));
}

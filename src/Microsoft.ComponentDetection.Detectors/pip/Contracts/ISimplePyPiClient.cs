#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.IO;
using System.Threading.Tasks;

public interface ISimplePyPiClient
{
    /// <summary>
    /// Uses the release url to retrieve the project file.
    /// </summary>
    /// <param name="releaseUrl">The url to fetch dependencies from. </param>
    /// <returns>Returns a project from the simplepypi api. </returns>
    Task<Stream> FetchPackageFileStreamAsync(Uri releaseUrl);

    /// <summary>
    /// Calls simplepypi and retrieves the project specified with the spec name.
    /// </summary>
    /// <param name="spec">The PipDependencySpecification for the project. </param>
    /// <returns>Returns a project from the simplepypi api. </returns>
    Task<SimplePypiProject> GetSimplePypiProjectAsync(PipDependencySpecification spec);
}

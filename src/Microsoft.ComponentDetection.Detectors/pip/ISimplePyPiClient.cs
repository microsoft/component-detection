using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Detectors.Tests")]

namespace Microsoft.ComponentDetection.Detectors.Pip;

public interface ISimplePyPiClient
{
    /// <summary>
    /// Uses the release url to retrieve the project file.
    /// </summary>
    /// <param name="name">The package name. </param>
    /// <param name="version">The package version. </param>
    /// <param name="release">The PythonProjectRelease. </param>
    /// <returns>Returns a project from the simplepypi api. </returns>
    Task<Stream> FetchPackageFileStreamAsync(string name, string version, PythonProjectRelease release);

    /// <summary>
    /// Calls simplepypi and retrieves the project specified with the spec name.
    /// </summary>
    /// <param name="spec">The PipDependencySpecification for the project. </param>
    /// <returns>Returns a project from the simplepypi api. </returns>
    Task<SimplePypiProject> GetSimplePypiProjectAsync(PipDependencySpecification spec);
}

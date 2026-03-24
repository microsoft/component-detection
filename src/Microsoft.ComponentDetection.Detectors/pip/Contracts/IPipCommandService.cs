#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public interface IPipCommandService
{
    /// <summary>
    /// Checks the existence of pip.
    /// </summary>
    /// <param name="pipPath">Optional override of the pip.exe absolute path.</param>
    /// <param name="pythonPath">Optional override of the python.exe absolute path.</param>
    /// <returns>True if pip is found on the OS PATH.</returns>
    Task<bool> PipExistsAsync(string pipPath = null, string pythonPath = null);

    /// <summary>
    /// Retrieves the version of pip from the given path. PythonVersion allows for loose version strings such as "1".
    /// </summary>
    /// <param name="pipPath">Optional override of the pip.exe absolute path.</param>
    /// <param name="pythonPath">Optional override of the python.exe absolute path.</param>
    /// <returns>Version of pip.</returns>
    Task<Version> GetPipVersionAsync(string pipPath = null, string pythonPath = null);

    /// <summary>
    /// Generates a pip installation report for a given setup.py or requirements.txt file.
    /// </summary>
    /// <param name="path">Path of the Python requirements file.</param>
    /// <param name="pipExePath">Optional override of the pip.exe absolute path.</param>
    /// <param name="pythonExePath">Optional override of the python.exe absolute path.</param>
    /// <param name="cancellationToken">Token used for canceling the installation report generation.</param>
    /// <returns>See https://pip.pypa.io/en/stable/reference/installation-report/#specification.</returns>
    Task<(PipInstallationReport Report, FileInfo ReportFile)> GenerateInstallationReportAsync(string path, string pipExePath = null, string pythonExePath = null, CancellationToken cancellationToken = default);
}

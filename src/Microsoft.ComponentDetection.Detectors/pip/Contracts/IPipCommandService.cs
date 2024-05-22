namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.IO;
using System.Threading.Tasks;

public interface IPipCommandService
{
    Task<bool> PipExistsAsync(string pipPath = null);

    Task<(PipInstallationReport Report, FileInfo ReportFile)> GenerateInstallationReportAsync(string path, string pipExePath = null);
}

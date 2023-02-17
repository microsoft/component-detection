namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public interface IYarnLockFileFactory
{
    Task<YarnLockFile> ParseYarnLockFileAsync(Stream file, ILogger logger);
}

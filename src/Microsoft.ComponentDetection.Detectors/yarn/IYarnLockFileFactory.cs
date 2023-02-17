namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

public interface IYarnLockFileFactory
{
    Task<YarnLockFile> ParseYarnLockFileAsync(ISingleFileComponentRecorder singleFileComponentRecorder, Stream file, ILogger logger);
}

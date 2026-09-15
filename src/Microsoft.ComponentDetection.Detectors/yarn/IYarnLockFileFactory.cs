#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public interface IYarnLockFileFactory
{
    Task<YarnLockFile> ParseYarnLockFileAsync(ISingleFileComponentRecorder singleFileComponentRecorder, Stream file, ILogger logger);
}

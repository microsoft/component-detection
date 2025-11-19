#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;

public interface IGoParser
{
    Task<bool> ParseAsync(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream file, GoGraphTelemetryRecord record);
}

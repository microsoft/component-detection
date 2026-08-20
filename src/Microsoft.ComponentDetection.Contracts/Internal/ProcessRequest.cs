#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Internal;

public class ProcessRequest
{
    public IComponentStream ComponentStream { get; set; }

    public ISingleFileComponentRecorder SingleFileComponentRecorder { get; set; }
}

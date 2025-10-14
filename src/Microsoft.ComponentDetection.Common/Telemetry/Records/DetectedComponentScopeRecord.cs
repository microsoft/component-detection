#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System.Runtime.CompilerServices;

public class DetectedComponentScopeRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "ComponentScopeRecord";

    public int? MavenProvidedScopeCount { get; set; } = 0;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void IncrementProvidedScopeCount()
    {
        this.MavenProvidedScopeCount++;
    }
}

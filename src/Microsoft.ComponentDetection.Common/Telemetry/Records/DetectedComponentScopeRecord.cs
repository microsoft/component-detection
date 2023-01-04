using System.Runtime.CompilerServices;

namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

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

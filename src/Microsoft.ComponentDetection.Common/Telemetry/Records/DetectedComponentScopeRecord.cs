namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System.Runtime.CompilerServices;

internal class DetectedComponentScopeRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "ComponentScopeRecord";

    public int? MavenProvidedScopeCount { get; set; } = 0;

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void IncrementProvidedScopeCount()
    {
        this.MavenProvidedScopeCount++;
    }
}

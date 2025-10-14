#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;

public class DependencyGraphTranslationRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "DependencyGraphTranslationRecord";

    public string DetectorId { get; set; }

    public TimeSpan? TimeToAddRoots { get; set; }

    public TimeSpan? TimeToAddAncestors { get; set; }
}
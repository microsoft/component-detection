#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Represents a component with unresolved variables that needs second-pass processing.
/// </summary>
internal record PendingComponent(
    string GroupId,
    string ArtifactId,
    string VersionTemplate,
    ISingleFileComponentRecorder Recorder,
    string FilePath);

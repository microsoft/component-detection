namespace Microsoft.ComponentDetection.Detectors.Linux;

/// <summary>
/// Marker interface for the Docker-based Syft runner.
/// Runs Syft by executing a Docker container with the Syft image.
/// </summary>
public interface IDockerSyftRunner : ISyftRunner
{
}

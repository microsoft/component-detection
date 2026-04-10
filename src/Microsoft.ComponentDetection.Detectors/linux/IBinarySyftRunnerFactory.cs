namespace Microsoft.ComponentDetection.Detectors.Linux;

/// <summary>
/// Factory for creating binary-based Syft runners.
/// </summary>
public interface IBinarySyftRunnerFactory
{
    /// <summary>
    /// Creates a binary Syft runner configured to use the specified binary path.
    /// </summary>
    /// <param name="binaryPath">The path to the Syft binary.</param>
    /// <returns>An <see cref="ISyftRunner"/> that invokes the specified Syft binary.</returns>
    ISyftRunner Create(string binaryPath);
}

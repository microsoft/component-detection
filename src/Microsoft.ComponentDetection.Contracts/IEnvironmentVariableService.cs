namespace Microsoft.ComponentDetection.Contracts;

/// <summary>
/// A service for querying environment variables.
/// </summary>
public interface IEnvironmentVariableService
{
    /// <summary>
    /// Checks if the environment variable <paramref name="name"/> exists.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns><c>true</c> if the environment variable exists, else, <c>false</c>.</returns>
    bool DoesEnvironmentVariableExist(string name);

    /// <summary>
    /// Gets the value of the environment variable <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns>The value of the environment variable.</returns>
    string GetEnvironmentVariable(string name);

    /// <summary>
    /// Checks if the environment variable <paramref name="name"/> is true.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns><c>true</c> if the environment variable is true, else, <c>false</c>.</returns>
    bool IsEnvironmentVariableValueTrue(string name);
}

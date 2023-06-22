namespace Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Wraps some common environment variable operations for easier testability.
/// </summary>
public interface IEnvironmentVariableService
{
    /// <summary>
    /// Returns true if the environment variable exists.
    /// </summary>
    /// <param name="name">Name of the environment variable.</param>
    /// <returns>Returns true if the environment variable exists, otherwise false.</returns>
    bool DoesEnvironmentVariableExist(string name);

    /// <summary>
    /// Returns the value of the environment variable.
    /// </summary>
    /// <param name="name">Name of the environment variable.</param>
    /// <returns>Returns a string of the environment variable value.</returns>
    string GetEnvironmentVariable(string name);

    /// <summary>
    /// Returns true if the environment variable value is true.
    /// </summary>
    /// <param name="name">Name of the environment variable.</param>
    /// <returns>Returns true if the environment variable value is true, otherwise false.</returns>
    bool IsEnvironmentVariableValueTrue(string name);
}

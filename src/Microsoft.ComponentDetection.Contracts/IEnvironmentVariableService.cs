#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;

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
    /// Returns the value of an environment variable which is formatted as a delimited list.
    /// </summary>
    /// <param name="name">Name of the environment variable.</param>
    /// <param name="delimiter">Delimiter separating the items in the list.</param>
    /// <returns>Returns she parsed environment variable value.</returns>
    List<string> GetListEnvironmentVariable(string name, string delimiter = ",");

    /// <summary>
    /// Returns true if the environment variable value is true.
    /// </summary>
    /// <param name="name">Name of the environment variable.</param>
    /// <returns>Returns true if the environment variable value is true, otherwise false.</returns>
    bool IsEnvironmentVariableValueTrue(string name);
}

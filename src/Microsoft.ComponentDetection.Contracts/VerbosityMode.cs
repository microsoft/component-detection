namespace Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Represents the verbosity mode of Component Detection.
/// </summary>
public enum VerbosityMode
{
    /// <summary>
    /// Quiet mode.
    /// Only errors are displayed.
    /// </summary>
    Quiet = 0,

    /// <summary>
    /// Normal mode.
    /// Errors, warnings and information are displayed.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Verbose mode.
    /// Errors, warnings, information and debug messages are displayed.
    /// </summary>
    Verbose = 2,
}

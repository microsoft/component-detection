namespace Microsoft.ComponentDetection.Detectors.Maven;

/// <summary>
/// Represents the result of executing a Maven CLI command.
/// </summary>
/// <param name="Success">Whether the command completed successfully.</param>
/// <param name="ErrorOutput">The error output from the command, if any.</param>
public record MavenCliResult(bool Success, string ErrorOutput);

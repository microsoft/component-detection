namespace Microsoft.ComponentDetection.Detectors.Maven;

/// <summary>
/// Represents the result of executing a Maven CLI command.
/// </summary>
/// <param name="Success">Whether the command completed successfully.</param>
/// <param name="ErrorOutput">The error output from the command, if any.</param>
/// <param name="DependenciesFileContent">The content of the generated dependencies file, if successful.</param>
public record MavenCliResult(bool Success, string ErrorOutput, string? DependenciesFileContent = null);

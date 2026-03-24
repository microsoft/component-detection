#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for managing execution on a command line. Generally, methods on this service expect a command line environment to be
/// where the detection tool is running, so all logic relying on them should gate on .IsCommandLineExecution().
/// </summary>
public interface ICommandLineInvocationService
{
    /// <summary>
    /// Used to gate logic that requires a command line execution environment.
    /// </summary>
    /// <returns>True if it is a command line execution environment, false otherwise.</returns>
    bool IsCommandLineExecution();

    /// <summary>
    /// Checks to see if the given command can be located -- in cases of absolute paths, this is a simple File.Exists. For non-absolute paths, all PATH entries are checked.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="workingDirectory">The directory under which to execute the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with true if the command can be found in the local environment, false otherwise.</returns>
    Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters);

    /// <summary>
    /// Checks to see if the given command can be located -- in cases of absolute paths, this is a simple File.Exists. For non-absolute paths, all PATH entries are checked.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with true if the command can be found in the local environment, false otherwise.</returns>
    Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If the command has not been located yet, CanCommandBeLocated will be invoked without the submitted parameters.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="workingDirectory">The directory under which to run the command.</param>
    /// <param name="cancellationToken">Token used for cancelling the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, CancellationToken cancellationToken = default, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If the command has not been located yet, CanCommandBeLocated will be invoked without the submitted parameters.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    [Obsolete($"This implementation of {nameof(ExecuteCommandAsync)} is deprecated. Please use a version with CancellationTokens.")]
    Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If the command has not been located yet, CanCommandBeLocated will be invoked without the submitted parameters.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="workingDirectory">The directory under which to run the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    [Obsolete($"This implementation of {nameof(ExecuteCommandAsync)} is deprecated. Please use a version with CancellationTokens.")]
    Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If the command has not been located yet, CanCommandBeLocated will be invoked without the submitted parameters.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="cancellationToken">Token used for cancelling the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, CancellationToken cancellationToken = default, params string[] parameters);
}

/// <summary>
/// Represents the result of executing a command line command.
/// </summary>
public class CommandLineExecutionResult
{
    /// <summary>
    /// Gets or sets all standard output for the process execution.
    /// </summary>
    public string StdOut { get; set; }

    /// <summary>
    /// Gets or sets all standard error output for the process execution.
    /// </summary>
    public string StdErr { get; set; }

    /// <summary>
    /// Gets or sets the process exit code for the executed process.
    /// </summary>
    public int ExitCode { get; set; }
}

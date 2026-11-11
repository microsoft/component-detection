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
    public bool IsCommandLineExecution();

    /// <summary>
    /// Executes candidate commands until one exits successfully, then caches the working command.
    /// Cancellation stops the current probe's process tree and the wait. Overloads without a token use CancellationToken.None.
    /// </summary>
    /// <param name="command">The command name to execute.</param>
    /// <param name="additionalCandidateCommands">Alternative commands with the same calling pattern.</param>
    /// <param name="workingDirectory">The directory under which to execute the probe.</param>
    /// <param name="cancellationToken">Token used for cancelling command lookup.</param>
    /// <param name="parameters">Arguments to pass to each candidate command.</param>
    /// <returns>True if a candidate command exits successfully, false otherwise.</returns>
    public Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands, DirectoryInfo workingDirectory, CancellationToken cancellationToken, params string[] parameters);

    /// <summary>
    /// Executes candidate commands until one exits successfully, then caches the working command. Uses CancellationToken.None.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="workingDirectory">The directory under which to execute the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with true if the command can be found in the local environment, false otherwise.</returns>
    public Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters);

    /// <summary>
    /// Executes candidate commands until one exits successfully, then caches the working command. Uses CancellationToken.None.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with true if the command can be found in the local environment, false otherwise.</returns>
    public Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If not cached, the same arguments are first used to probe candidate commands.
    /// The cancellation token applies to both the availability probe and execution.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="workingDirectory">The directory under which to run the command.</param>
    /// <param name="cancellationToken">Token used for cancelling the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    public Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, CancellationToken cancellationToken = default, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If not cached, the same arguments are first used to probe candidate commands.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    [Obsolete($"This implementation of {nameof(ExecuteCommandAsync)} is deprecated. Please use a version with CancellationTokens.")]
    public Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If not cached, the same arguments are first used to probe candidate commands.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="workingDirectory">The directory under which to run the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    [Obsolete($"This implementation of {nameof(ExecuteCommandAsync)} is deprecated. Please use a version with CancellationTokens.")]
    public Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters);

    /// <summary>
    /// Executes a command line command. If not cached, the same arguments are first used to probe candidate commands.
    /// The cancellation token applies to both the availability probe and execution.
    /// </summary>
    /// <param name="command">The command name to execute. Environment variables like PATH on windows will also be considered if the command is not an absolute path. </param>
    /// <param name="additionalCandidateCommands">Other commands that could satisfy the need for the first command. Assumption is that they all share similar calling patterns.</param>
    /// <param name="cancellationToken">Token used for cancelling the command.</param>
    /// <param name="parameters">The parameters that should be passed to the command. The parameters will be space-joined.</param>
    /// <returns>Awaitable task with the result of executing the command, including exit code.</returns>
    public Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, CancellationToken cancellationToken = default, params string[] parameters);
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

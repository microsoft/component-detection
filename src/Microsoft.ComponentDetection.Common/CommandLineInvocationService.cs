#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;

/// <inheritdoc/>
public class CommandLineInvocationService : ICommandLineInvocationService
{
    private readonly IDictionary<string, string> commandLocatableCache = new ConcurrentDictionary<string, string>();

    /// <inheritdoc/>
    public async Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters)
    {
        additionalCandidateCommands ??= [];
        parameters ??= [];
        var allCommands = new[] { command }.Concat(additionalCandidateCommands);
        if (!this.commandLocatableCache.TryGetValue(command, out var validCommand))
        {
            foreach (var commandToTry in allCommands)
            {
                using var record = new CommandLineInvocationTelemetryRecord();

                var joinedParameters = string.Join(" ", parameters);
                try
                {
                    var result = await RunProcessAsync(commandToTry, joinedParameters, workingDirectory);
                    record.Track(result, commandToTry, joinedParameters);

                    if (result.ExitCode == 0)
                    {
                        this.commandLocatableCache[command] = validCommand = commandToTry;
                        break;
                    }
                }
                catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is PlatformNotSupportedException)
                {
                    // When we get an exception indicating the command cannot be found.
                    record.Track(ex, commandToTry, joinedParameters);
                }
            }
        }

        return !string.IsNullOrWhiteSpace(validCommand);
    }

    /// <inheritdoc/>
    public async Task<CommandLineExecutionResult> ExecuteCommandAsync(
        string command,
        IEnumerable<string> additionalCandidateCommands = null,
        DirectoryInfo workingDirectory = null,
        CancellationToken cancellationToken = default,
        params string[] parameters)
    {
        var isCommandLocatable = await this.CanCommandBeLocatedAsync(command, additionalCandidateCommands, workingDirectory, parameters);
        if (!isCommandLocatable)
        {
            throw new InvalidOperationException(
                $"{nameof(this.ExecuteCommandAsync)} was called with a command that could not be located: `{command}`!");
        }

        if (workingDirectory != null && !Directory.Exists(workingDirectory.FullName))
        {
            throw new InvalidOperationException(
                $"{nameof(this.ExecuteCommandAsync)} was called with a working directory that could not be located: `{workingDirectory.FullName}`");
        }

        using var record = new CommandLineInvocationTelemetryRecord();

        var pathToRun = this.commandLocatableCache[command];
        var joinedParameters = string.Join(" ", parameters);
        var commandForLogging = joinedParameters.RemoveSensitiveInformation();
        try
        {
            var result = await RunProcessAsync(pathToRun, joinedParameters, workingDirectory, cancellationToken);
            record.Track(result, pathToRun, commandForLogging);
            return result;
        }
        catch (Exception ex)
        {
            record.Track(ex, pathToRun, commandForLogging);
            throw;
        }
    }

    /// <inheritdoc/>
    public bool IsCommandLineExecution()
    {
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters)
    {
        return await this.CanCommandBeLocatedAsync(command, additionalCandidateCommands, workingDirectory: null, parameters);
    }

    /// <inheritdoc/>
    public async Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, CancellationToken cancellationToken = default, params string[] parameters)
    {
        return await this.ExecuteCommandAsync(command, additionalCandidateCommands, workingDirectory: null, cancellationToken, parameters);
    }

    /// <inheritdoc/>
    public async Task<CommandLineExecutionResult> ExecuteCommandAsync(
        string command,
        IEnumerable<string> additionalCandidateCommands = null,
        DirectoryInfo workingDirectory = null,
        params string[] parameters)
    {
        return await this.ExecuteCommandAsync(command, additionalCandidateCommands, workingDirectory, CancellationToken.None, parameters);
    }

    /// <inheritdoc/>
    public async Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters)
    {
        return await this.ExecuteCommandAsync(command, additionalCandidateCommands, workingDirectory: null, CancellationToken.None, parameters);
    }

    private static Task<CommandLineExecutionResult> RunProcessAsync(string fileName, string parameters, DirectoryInfo workingDirectory = null)
    {
        return RunProcessAsync(fileName, parameters, workingDirectory, CancellationToken.None);
    }

    private static Task<CommandLineExecutionResult> RunProcessAsync(string fileName, string parameters, DirectoryInfo workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<CommandLineExecutionResult>();

        if (fileName.EndsWith(".cmd") || fileName.EndsWith(".bat"))
        {
            // If a script attempts to find its location using "%dp0", that can return the wrong path (current
            // working directory) unless the script is run via "cmd /C".  An example is "ant.bat".
            parameters = $"/C {fileName} {parameters}";
            fileName = "cmd.exe";
        }

        var process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = parameters,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            },
            EnableRaisingEvents = true,
        };

        if (workingDirectory != null)
        {
            process.StartInfo.WorkingDirectory = workingDirectory.FullName;
        }

        var errorText = string.Empty;
        var stdOutText = string.Empty;

        var t1 = new Task(() =>
        {
            errorText = process.StandardError.ReadToEnd();
        });
        var t2 = new Task(() =>
        {
            stdOutText = process.StandardOutput.ReadToEnd();
        });

        process.Exited += (sender, args) =>
        {
            Task.WaitAll(t1, t2);
            tcs.TrySetResult(new CommandLineExecutionResult { ExitCode = process.ExitCode, StdErr = errorText, StdOut = stdOutText });
            process.Dispose();
        };

        process.Start();
        t1.Start();
        t2.Start();

        cancellationToken.Register(() =>
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // swallow invalid operations, which indicate that there is no process associated with
                // the process object, and therefore nothing to kill
                // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-8.0#system-diagnostics-process-kill
                return;
            }
        });

        return tcs.Task;
    }
}

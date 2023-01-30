namespace Microsoft.ComponentDetection.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;

[Export(typeof(ICommandLineInvocationService))]
public class CommandLineInvocationService : ICommandLineInvocationService
{
    private readonly IDictionary<string, string> commandLocatableCache = new ConcurrentDictionary<string, string>();

    public async Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters)
    {
        additionalCandidateCommands ??= Enumerable.Empty<string>();
        parameters ??= new string[0];
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

    public async Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, DirectoryInfo workingDirectory = null, params string[] parameters)
    {
        var isCommandLocatable = await this.CanCommandBeLocatedAsync(command, additionalCandidateCommands);
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
        try
        {
            var result = await RunProcessAsync(pathToRun, joinedParameters, workingDirectory);
            record.Track(result, pathToRun, joinedParameters);
            return result;
        }
        catch (Exception ex)
        {
            record.Track(ex, pathToRun, joinedParameters);
            throw;
        }
    }

    public bool IsCommandLineExecution()
    {
        return true;
    }

    public async Task<bool> CanCommandBeLocatedAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters)
    {
        return await this.CanCommandBeLocatedAsync(command, additionalCandidateCommands, workingDirectory: null, parameters);
    }

    public async Task<CommandLineExecutionResult> ExecuteCommandAsync(string command, IEnumerable<string> additionalCandidateCommands = null, params string[] parameters)
    {
        return await this.ExecuteCommandAsync(command, additionalCandidateCommands, workingDirectory: null, parameters);
    }

    private static Task<CommandLineExecutionResult> RunProcessAsync(string fileName, string parameters, DirectoryInfo workingDirectory = null)
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
            tcs.SetResult(new CommandLineExecutionResult { ExitCode = process.ExitCode, StdErr = errorText, StdOut = stdOutText });
            process.Dispose();
        };

        process.Start();
        t1.Start();
        t2.Start();

        return tcs.Task;
    }
}

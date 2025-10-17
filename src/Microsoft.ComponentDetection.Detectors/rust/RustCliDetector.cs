namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

/// <summary>
/// A Rust CLI detector that uses the cargo metadata command to detect Rust components.
/// </summary>
public class RustCliDetector : FileComponentDetector
{
    private readonly IRustCliParser cliParser;
    private readonly IRustCargoLockParser cargoLockParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="RustCliDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The component stream enumerable factory.</param>
    /// <param name="walkerFactory">The walker factory.</param>
    /// <param name="cliService">The command line invocation service.</param>
    /// <param name="envVarService">The environment variable reader service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cliParser">Rust cli parser.</param>
    /// <param name="cargoLockParser">Rust cargo lock parser.</param>
    public RustCliDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService cliService,
        IEnvironmentVariableService envVarService,
        ILogger<RustCliDetector> logger,
        IRustCliParser cliParser,
        IRustCargoLockParser cargoLockParser)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
        this.cliParser = cliParser;
        this.cargoLockParser = cargoLockParser;
    }

    /// <inheritdoc />
    public override string Id => "RustCli";

    /// <inheritdoc />
    public override IEnumerable<string> Categories { get; } = ["Rust"];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Cargo];

    /// <inheritdoc />
    public override int Version => 4;

    /// <inheritdoc />
    public override IList<string> SearchPatterns { get; } = ["Cargo.toml"];

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var componentStream = processRequest.ComponentStream;
        this.Logger.LogInformation("Discovered Cargo.toml: {Location}", componentStream.Location);

        using var record = new RustGraphTelemetryRecord();
        record.CargoTomlLocation = processRequest.ComponentStream.Location;

        try
        {
            // Try to parse using cargo metadata command
            var parseResult = await this.cliParser.ParseAsync(
                componentStream,
                processRequest.SingleFileComponentRecorder,
                cancellationToken);

            if (parseResult.Success)
            {
                // CLI parsing succeeded
                record.DidRustCliCommandFail = false;
                record.WasRustFallbackStrategyUsed = false;
            }
            else
            {
                // CLI parsing failed
                record.DidRustCliCommandFail = true;
                record.RustCliCommandError = parseResult.ErrorMessage;
                record.FallbackReason = parseResult.FailureReason;

                // Determine if we should use fallback based on the error
                if (!string.IsNullOrEmpty(parseResult.ErrorMessage))
                {
                    record.WasRustFallbackStrategyUsed = ShouldFallbackFromError(parseResult.ErrorMessage);
                }
                else
                {
                    // If there's no error message (e.g., manually disabled or cargo not found), use fallback
                    record.WasRustFallbackStrategyUsed = true;
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Failed attempting to call `cargo` with file: {Location}", processRequest.ComponentStream.Location);
            record.DidRustCliCommandFail = true;
            record.RustCliCommandError = e.Message;
            record.WasRustFallbackStrategyUsed = true;
            record.FallbackReason = "InvalidOperationException";
        }
        finally
        {
            if (record.WasRustFallbackStrategyUsed)
            {
                try
                {
                    await this.ProcessCargoLockFallbackAsync(componentStream, processRequest.SingleFileComponentRecorder, record, cancellationToken);
                }
                catch (ArgumentException e)
                {
                    this.Logger.LogWarning(e, "fallback failed for {Location}", processRequest.ComponentStream.Location);
                    record.DidRustCliCommandFail = true;
                    record.RustCliCommandError = e.Message;
                    record.WasRustFallbackStrategyUsed = true;
                }

                this.AdditionalProperties.Add(("Rust Fallback", JsonConvert.SerializeObject(record)));
            }
        }
    }

    private static bool ShouldFallbackFromError(string error)
    {
        if (error.Contains("current package believes it's in a workspace", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private IComponentStream FindCorrespondingCargoLock(IComponentStream cargoToml)
    {
        var cargoLockStream = this.ComponentStreamEnumerableFactory.GetComponentStreams(
            new FileInfo(cargoToml.Location).Directory,
            ["Cargo.lock"],
            (name, directoryName) => false,
            recursivelyScanDirectories: false).FirstOrDefault();

        if (cargoLockStream == null)
        {
            return null;
        }

        if (cargoLockStream.Stream.CanRead)
        {
            return cargoLockStream;
        }
        else
        {
            var fileStream = new FileStream(cargoLockStream.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new ComponentStream()
            {
                Location = cargoLockStream.Location,
                Pattern = cargoLockStream.Pattern,
                Stream = fileStream,
            };
        }
    }

    private async Task ProcessCargoLockFallbackAsync(
        IComponentStream cargoTomlFile,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        RustGraphTelemetryRecord record,
        CancellationToken cancellationToken = default)
    {
        var cargoLockFileStream = this.FindCorrespondingCargoLock(cargoTomlFile);
        if (cargoLockFileStream == null)
        {
            this.Logger.LogWarning("Fallback failed, could not find Cargo.lock file for {CargoTomlLocation}, skipping processing", cargoTomlFile.Location);
            record.FallbackCargoLockFound = false;
            return;
        }
        else
        {
            this.Logger.LogWarning("Falling back to cargo.lock processing using {CargoTomlLocation}", cargoLockFileStream.Location);
        }

        record.FallbackCargoLockLocation = cargoLockFileStream.Location;
        record.FallbackCargoLockFound = true;

        // Use RustCrateParser to parse the Cargo.lock file
        var lockfileVersion = await this.cargoLockParser.ParseAsync(
            cargoLockFileStream,
            singleFileComponentRecorder,
            cancellationToken);

        if (lockfileVersion.HasValue)
        {
            this.RecordLockfileVersion(lockfileVersion.Value);
        }
    }
}

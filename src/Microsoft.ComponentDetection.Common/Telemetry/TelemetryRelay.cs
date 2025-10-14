#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;

/// <summary>
/// Singleton that is responsible for relaying telemetry records to Telemetry Services.
/// </summary>
public sealed class TelemetryRelay
{
    private IEnumerable<ITelemetryService> telemetryServices;

    // For things not populating the telemetry services collection, let's not throw.
    private TelemetryRelay() =>
        this.telemetryServices = [];

    /// <summary>
    /// Gets a value indicating whether or not the telemetry relay has been shutdown.
    /// </summary>
    public static bool Active { get; private set; } = true;

    /// <summary>
    /// Gets the singleton.
    /// </summary>
    public static TelemetryRelay Instance { get; } = new TelemetryRelay();

    public void Init(IEnumerable<ITelemetryService> telemetryServices) => this.telemetryServices = telemetryServices;

    /// <summary>
    /// Post a given telemetry record to all telemetry services.
    /// </summary>
    /// <param name="record">Record to post. </param>
    public void PostTelemetryRecord(IDetectionTelemetryRecord record)
    {
        foreach (var service in this.telemetryServices)
        {
            try
            {
                service.PostRecord(record);
            }
            catch
            {
                // Telemetry should never crash the application
            }
        }
    }

    /// <summary>
    /// Disables the sending of telemetry and flushes any messages out of the queue for each service.
    /// </summary>
    /// <returns><see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ShutdownAsync()
    {
        Active = false;

        foreach (var service in this.telemetryServices)
        {
            try
            {
                // Set a timeout for services that flush synchronously.
                await AsyncExecution.ExecuteVoidWithTimeoutAsync(
                    () => service.Flush(),
                    TimeSpan.FromSeconds(20),
                    CancellationToken.None);
            }
            catch
            {
                Console.WriteLine("Logging output failed");
            }
        }
    }

    public void SetTelemetryMode(TelemetryMode mode)
    {
        foreach (var telemetryService in this.telemetryServices ?? [])
        {
            telemetryService.SetMode(mode);
        }
    }
}

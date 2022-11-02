using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common.Telemetry.Records;

namespace Microsoft.ComponentDetection.Common.Telemetry
{
    /// <summary>
    /// Singleton that is responsible for relaying telemetry records to Telemetry Services.
    /// </summary>
    public sealed class TelemetryRelay
    {
        // For things not populating the telemetry services collection, let's not throw.
        private TelemetryRelay() =>
            TelemetryServices = Enumerable.Empty<ITelemetryService>();

        [ImportMany]
        public static IEnumerable<ITelemetryService> TelemetryServices { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not the telemetry relay has been shutdown.
        /// </summary>
        public static bool Active { get; private set; } = true;

        /// <summary>
        /// Gets the singleton.
        /// </summary>
        public static TelemetryRelay Instance { get; } = new TelemetryRelay();

        /// <summary>
        /// Post a given telemetry record to all telemetry services.
        /// </summary>
        /// <param name="record">Record to post. </param>
        public void PostTelemetryRecord(IDetectionTelemetryRecord record)
        {
            foreach (var service in TelemetryServices)
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

            foreach (var service in TelemetryServices)
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
            foreach (var telemetryService in TelemetryServices ?? Enumerable.Empty<ITelemetryService>())
            {
                telemetryService.SetMode(mode);
            }
        }
    }
}

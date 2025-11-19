#nullable disable
namespace Microsoft.ComponentDetection.Common.Telemetry.Records;

using System;

using System.Threading;
using System.Threading.Tasks;

public class BcdeExecutionTelemetryRecord : BaseDetectionTelemetryRecord
{
    public override string RecordName => "BcdeExecution";

    public string Command { get; set; }

    public int? ExitCode { get; set; }

    public int? HiddenExitCode { get; set; }

    public string UnhandledException { get; set; }

    public string Arguments { get; set; }

    public string ErrorMessage { get; set; }

    public string AgentOSMeaningfulDetails { get; set; }

    public string AgentOSDescription { get; set; }

    public static async Task<TReturn> TrackAsync<TReturn>(
        Func<BcdeExecutionTelemetryRecord, CancellationToken, Task<TReturn>> functionToTrack,
        bool terminalRecord = false,
        CancellationToken cancellationToken = default)
    {
        using var record = new BcdeExecutionTelemetryRecord();

        try
        {
            return await functionToTrack(record, cancellationToken);
        }
        catch (Exception ex)
        {
            record.UnhandledException = ex.ToString();
            throw;
        }
        finally
        {
            record.Dispose();
            if (terminalRecord && !(record.Command?.Equals("help", StringComparison.InvariantCultureIgnoreCase) ?? false))
            {
                await TelemetryRelay.Instance.ShutdownAsync();
            }
        }
    }
}

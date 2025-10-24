#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

/// <summary>Code used to communicate the state of a scan after completion.</summary>
public enum ProcessingResultCode
{
    /// <summary>The scan was completely successful.</summary>
    Success = 0,

    /// <summary>The scan had some detections complete while others encountered errors. The log file should indicate any issues that happened during the scan.</summary>
    PartialSuccess = 1,

    /// <summary>A critical error occurred during the scan.</summary>
    Error = 2,

    /// <summary>A critical error occurred during the scan, related to user input specifically.</summary>
    InputError = 3,

    /// <summary>The execution exceeded the expected amount of time to run.</summary>
    TimeoutError = 4,
}

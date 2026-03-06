namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System.Collections.Generic;

/// <summary>
/// Interface for processing MSBuild binary log files to extract project information.
/// </summary>
internal interface IBinLogProcessor
{
    /// <summary>
    /// Extracts project information from a binary log file.
    /// </summary>
    /// <param name="binlogPath">Path to the binary log file.</param>
    /// <returns>Collection of project information extracted from the binlog.</returns>
    IReadOnlyList<MSBuildProjectInfo> ExtractProjectInfo(string binlogPath);
}

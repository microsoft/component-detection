namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Interface for processing MSBuild binary log files to extract project information.
/// </summary>
internal interface IBinLogProcessor
{
    /// <summary>
    /// Extracts project information from a binary log file.
    /// All absolute paths in the returned <see cref="MSBuildProjectInfo"/> objects
    /// (e.g., <see cref="MSBuildProjectInfo.ProjectPath"/>, <see cref="MSBuildProjectInfo.ProjectAssetsFile"/>)
    /// are rebased to be relative to <paramref name="sourceDirectory"/> when the binlog was produced
    /// on a different machine.
    /// </summary>
    /// <param name="binlogPath">Path to the binary log file on the scanning machine.</param>
    /// <param name="sourceDirectory">
    /// The source directory on the scanning machine, used to rebase paths when the binlog
    /// was produced on a different machine. May be <c>null</c> to skip rebasing.
    /// </param>
    /// <param name="cancellationToken">Token to cancel binlog replay.</param>
    /// <returns>Collection of project information extracted from the binlog, with paths rebased.</returns>
    IReadOnlyList<MSBuildProjectInfo> ExtractProjectInfo(string binlogPath, string? sourceDirectory = null, CancellationToken cancellationToken = default);
}

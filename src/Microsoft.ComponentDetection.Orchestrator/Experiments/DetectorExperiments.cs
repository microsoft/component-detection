#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System;

/// <summary>
/// Enables or disables detector experiments in Component Detection.
/// </summary>
public static class DetectorExperiments
{
    /// <summary>
    /// Check to automatically proccess experiments.
    /// </summary>
    public static bool AutomaticallyProcessExperiments { get; set; } = true;

    /// <summary>
    /// Manually enables detector experiments.
    /// </summary>
    public static bool Enable { get; set; }

    private static bool EnvironmentEnabled =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CD_DETECTOR_EXPERIMENTS"));

    internal static bool AreExperimentsEnabled => Enable || EnvironmentEnabled;
}

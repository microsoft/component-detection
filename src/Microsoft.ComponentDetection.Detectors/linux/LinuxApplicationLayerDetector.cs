#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Experimental detector for Linux container images that captures application-level packages
/// in addition to system packages. This detector runs as an experiment to compare results with the base
/// Linux detector (which only scans system packages).
/// </summary>
/// <param name="linuxScanner">The Linux scanner service.</param>
/// <param name="dockerService">The Docker service.</param>
/// <param name="logger">The logger.</param>
public class LinuxApplicationLayerDetector(
    ILinuxScanner linuxScanner,
    IDockerService dockerService,
    ILogger<LinuxApplicationLayerDetector> logger
) : LinuxContainerDetector(linuxScanner, dockerService, logger), IExperimentalDetector
{
    /// <inheritdoc/>
    public new string Id => "LinuxApplicationLayer";

    /// <inheritdoc/>
    public new IEnumerable<string> Categories =>
        [
            Enum.GetName(typeof(DetectorClass), DetectorClass.Linux),
            Enum.GetName(typeof(DetectorClass), DetectorClass.Cargo),
            Enum.GetName(typeof(DetectorClass), DetectorClass.CocoaPods),
            Enum.GetName(typeof(DetectorClass), DetectorClass.Conda),
            Enum.GetName(typeof(DetectorClass), DetectorClass.GoMod),
            Enum.GetName(typeof(DetectorClass), DetectorClass.Maven),
            Enum.GetName(typeof(DetectorClass), DetectorClass.Npm),
            Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet),
            Enum.GetName(typeof(DetectorClass), DetectorClass.Pip),
            Enum.GetName(typeof(DetectorClass), DetectorClass.RubyGems),
        ];

    /// <inheritdoc/>
    public new IEnumerable<ComponentType> SupportedComponentTypes =>
        [
            ComponentType.Linux,
            ComponentType.Cargo,
            ComponentType.Conda,
            ComponentType.Go,
            ComponentType.Maven,
            ComponentType.Npm,
            ComponentType.NuGet,
            ComponentType.Pip,
            ComponentType.Pod,
            ComponentType.RubyGems,
        ];

    /// <inheritdoc/>
    protected override ISet<ComponentType> GetEnabledComponentTypes() =>
        new HashSet<ComponentType>
        {
            ComponentType.Linux,
            ComponentType.Cargo,
            ComponentType.Conda,
            ComponentType.Go,
            ComponentType.Maven,
            ComponentType.Npm,
            ComponentType.NuGet,
            ComponentType.Pip,
            ComponentType.Pod,
            ComponentType.RubyGems,
        };
}

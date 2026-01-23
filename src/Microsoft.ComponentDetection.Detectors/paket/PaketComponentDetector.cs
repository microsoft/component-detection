namespace Microsoft.ComponentDetection.Detectors.Paket;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects NuGet packages in paket.lock files.
/// Paket is a dependency manager for .NET that provides better control over package dependencies.
/// </summary>
public sealed class PaketComponentDetector : FileComponentDetector
{
    private static readonly Regex PackageLineRegex = new(@"^\s{4}(\S+)\s+\(([^\)]+)\)", RegexOptions.Compiled);
    private static readonly Regex DependencyLineRegex = new(@"^\s{6}(\S+)\s+\((.+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="PaketComponentDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The factory for handing back component streams to File detectors.</param>
    /// <param name="walkerFactory">The factory for creating directory walkers.</param>
    /// <param name="logger">The logger to use.</param>
    public PaketComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<PaketComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override IList<string> SearchPatterns => ["paket.lock"];

    /// <inheritdoc />
    public override string Id => "Paket";

    /// <inheritdoc />
    public override IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.NuGet];

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            using var reader = new StreamReader(processRequest.ComponentStream.Stream);

            var currentSection = string.Empty;
            string currentPackageName = null;
            string currentPackageVersion = null;
            DetectedComponent currentComponent = null;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Check if this is a section header (e.g., NUGET, GITHUB, HTTP)
                if (!line.StartsWith(' ') && line.Trim().Length > 0)
                {
                    currentSection = line.Trim();
                    currentPackageName = null;
                    currentPackageVersion = null;
                    currentComponent = null;
                    continue;
                }

                // Only process NUGET section for now
                if (!currentSection.Equals("NUGET", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if this is a remote line (source URL)
                if (line.TrimStart().StartsWith("remote:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if this is a package line (4 spaces indentation)
                var packageMatch = PackageLineRegex.Match(line);
                if (packageMatch.Success)
                {
                    currentPackageName = packageMatch.Groups[1].Value;
                    currentPackageVersion = packageMatch.Groups[2].Value;

                    currentComponent = new DetectedComponent(
                        new NuGetComponent(currentPackageName, currentPackageVersion));

                    singleFileComponentRecorder.RegisterUsage(
                        currentComponent,
                        isExplicitReferencedDependency: true);

                    continue;
                }

                // Check if this is a dependency line (6 spaces indentation)
                var dependencyMatch = DependencyLineRegex.Match(line);
                if (dependencyMatch.Success && currentComponent != null)
                {
                    var dependencyName = dependencyMatch.Groups[1].Value;
                    var dependencyVersionSpec = dependencyMatch.Groups[2].Value;

                    // Extract the actual version from the version specification
                    // Version specs can be like ">= 3.3.0" or "1.2.10"
                    var versionMatch = Regex.Match(dependencyVersionSpec, @"[\d\.]+");
                    if (versionMatch.Success)
                    {
                        var dependencyComponent = new DetectedComponent(
                            new NuGetComponent(dependencyName, versionMatch.Value));

                        singleFileComponentRecorder.RegisterUsage(
                            dependencyComponent,
                            isExplicitReferencedDependency: false,
                            parentComponentId: currentComponent.Component.Id);
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            this.Logger.LogWarning(e, "Failed to read paket.lock file {File}", processRequest.ComponentStream.Location);
        }
    }
}

namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects NuGet packages in Central Package Management files (Directory.Packages.props, packages.props, package.props).
/// </summary>
public sealed class NuGetCentralPackageManagementDetector : FileComponentDetector, IExperimentalDetector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetCentralPackageManagementDetector"/> class.
    /// </summary>
    /// <param name="componentStreamEnumerableFactory">The factory for handing back component streams to File detectors.</param>
    /// <param name="walkerFactory">The factory for creating directory walkers.</param>
    /// <param name="logger">The logger to use.</param>
    public NuGetCentralPackageManagementDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<NuGetCentralPackageManagementDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override IList<string> SearchPatterns => ["Directory.Packages.props", "packages.props", "package.props"];

    /// <inheritdoc />
    public override string Id => "NuGetCentralPackageManagement";

    /// <inheritdoc />
    public override IEnumerable<string> Categories =>
        [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.NuGet];

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var propsDocument = XDocument.Load(processRequest.ComponentStream.Stream);

            // Check if this is a Central Package Management file
            if (!this.IsCentralPackageManagementFile(propsDocument))
            {
                this.Logger.LogDebug("File {File} is not a Central Package Management file", processRequest.ComponentStream.Location);
                return Task.CompletedTask;
            }

            // Parse PackageVersion elements
            var packageVersionElements = propsDocument.Descendants("PackageVersion");
            foreach (var packageElement in packageVersionElements)
            {
                var packageId = packageElement.Attribute("Include")?.Value;
                var version = packageElement.Attribute("Version")?.Value;

                if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
                {
                    this.Logger.LogDebug("Skipping PackageVersion element with missing Include or Version attribute in {File}", processRequest.ComponentStream.Location);
                    continue;
                }

                var detectedComponent = new DetectedComponent(
                    new NuGetComponent(packageId, version));

                // All packages in Central Package Management files are explicitly referenced
                // since they define the centrally managed versions
                singleFileComponentRecorder.RegisterUsage(detectedComponent, true, null, isDevelopmentDependency: false);

                this.Logger.LogDebug(
                    "Detected NuGet package {PackageId} version {Version} in Central Package Management file {File}",
                    packageId,
                    version,
                    processRequest.ComponentStream.Location);
            }

            // Parse GlobalPackageReference elements
            var globalPackageElements = propsDocument.Descendants("GlobalPackageReference");
            foreach (var packageElement in globalPackageElements)
            {
                var packageId = packageElement.Attribute("Include")?.Value;
                var version = packageElement.Attribute("Version")?.Value;

                if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
                {
                    this.Logger.LogDebug("Skipping GlobalPackageReference element with missing Include or Version attribute in {File}", processRequest.ComponentStream.Location);
                    continue;
                }

                var detectedComponent = new DetectedComponent(
                    new NuGetComponent(packageId, version));

                // Global package references are explicitly referenced and typically development dependencies
                singleFileComponentRecorder.RegisterUsage(detectedComponent, true, null, isDevelopmentDependency: true);

                this.Logger.LogDebug(
                    "Detected global NuGet package {PackageId} version {Version} in Central Package Management file {File}",
                    packageId,
                    version,
                    processRequest.ComponentStream.Location);
            }
        }
        catch (Exception e) when (e is XmlException)
        {
            this.Logger.LogWarning(e, "Failed to parse Central Package Management file {File}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if the props file is a Central Package Management file by checking for the
    /// ManagePackageVersionsCentrally property or the presence of PackageVersion/GlobalPackageReference elements.
    /// </summary>
    /// <param name="propsDocument">The props file document to check.</param>
    /// <returns>True if this is a Central Package Management file, false otherwise.</returns>
    private bool IsCentralPackageManagementFile(XDocument propsDocument)
    {
        // Check for the ManagePackageVersionsCentrally property set to true
        var managePackageVersionsCentrally = propsDocument.Descendants("ManagePackageVersionsCentrally")
            .FirstOrDefault()?.Value?.Trim();

        if (string.Equals(managePackageVersionsCentrally, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for the presence of PackageVersion or GlobalPackageReference elements
        var hasPackageVersionElements = propsDocument.Descendants("PackageVersion").Any();
        var hasGlobalPackageElements = propsDocument.Descendants("GlobalPackageReference").Any();

        return hasPackageVersionElements || hasGlobalPackageElements;
    }
}

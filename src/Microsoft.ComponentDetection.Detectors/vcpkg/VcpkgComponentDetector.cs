namespace Microsoft.ComponentDetection.Detectors.Vcpkg;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class VcpkgComponentDetector : FileComponentDetector
{
    private readonly HashSet<string> projectRoots = [];
    private readonly ConcurrentDictionary<string, ManifestInfo> manifestMappings = new(StringComparer.OrdinalIgnoreCase);

    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly IEnvironmentVariableService envVarService;

    public VcpkgComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ICommandLineInvocationService commandLineInvocationService,
        IEnvironmentVariableService environmentVariableService,
        ILogger<VcpkgComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.commandLineInvocationService = commandLineInvocationService;
        this.envVarService = environmentVariableService;
        this.Logger = logger;
    }

    public override string Id { get; } = "Vcpkg";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Vcpkg)];

    public override IList<string> SearchPatterns { get; } = ["vcpkg.spdx.json", "manifest-info.json"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Vcpkg];

    public override int Version => 2;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogDebug("vcpkg detector found {File}", file);

        var projectRootDirectory = Directory.GetParent(file.Location);
        if (this.projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
        {
            return;
        }

        await this.ParseSpdxFileAsync(this.GetManifestComponentRecorder(singleFileComponentRecorder), file);
    }

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var filteredProcessRequests = new List<ProcessRequest>();

        await processRequests.ForEachAsync(async pr =>
        {
            var fileLocation = pr.ComponentStream.Location;
            var fileName = Path.GetFileName(fileLocation);

            if (fileName.Equals("manifest-info.json", StringComparison.OrdinalIgnoreCase))
            {
                this.Logger.LogDebug("Discovered VCPKG package manifest file at: {Location}", pr.ComponentStream.Location);

                using (var reader = new StreamReader(pr.ComponentStream.Stream))
                {
                    var contents = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var manifestData = JsonConvert.DeserializeObject<ManifestInfo>(contents);

                    if (manifestData == null || string.IsNullOrWhiteSpace(manifestData.ManifestPath))
                    {
                        this.Logger.LogWarning("Failed to deserialize manifest-info.json or missing ManifestPath at {Path}", pr.ComponentStream.Location);
                    }
                    else
                    {
                        this.manifestMappings.TryAdd(fileLocation, manifestData);
                    }
                }
            }
            else
            {
                filteredProcessRequests.Add(pr);
            }
        }).ConfigureAwait(false);

        return filteredProcessRequests.ToObservable();
    }

    private async Task ParseSpdxFileAsync(
        ISingleFileComponentRecorder singleFileComponentRecorder,
        IComponentStream file)
    {
        using var reader = new StreamReader(file.Stream);
        VcpkgSBOM sbom;
        try
        {
            sbom = JsonConvert.DeserializeObject<VcpkgSBOM>(await reader.ReadToEndAsync());
        }
        catch (Exception)
        {
            return;
        }

        if (sbom?.Packages == null)
        {
            return;
        }

        foreach (var item in sbom.Packages)
        {
            try
            {
                if (string.IsNullOrEmpty(item.Name))
                {
                    continue;
                }

                this.Logger.LogDebug("vcpkg parsed package {PackageName}", item.Name);
                if (item.SPDXID == "SPDXRef-port")
                {
                    var split = item.VersionInfo.Split('#');
                    var component = new VcpkgComponent(item.SPDXID, item.Name, split[0], portVersion: split.Length >= 2 ? split[1] : "0", downloadLocation: item.DownloadLocation);
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                }
                else if (item.SPDXID == "SPDXRef-binary")
                {
                    var split = item.Name.Split(':');
                    var component = new VcpkgComponent(item.SPDXID, item.Name, item.VersionInfo, triplet: split[1], downloadLocation: item.DownloadLocation);
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                }
                else if (item.SPDXID.StartsWith("SPDXRef-resource-"))
                {
                    var dl = item.DownloadLocation;
                    var split = dl.Split("#");
                    var subpath = split.Length > 1 ? split[1] : null;
                    dl = split.Length > 1 ? split[0] : dl;
                    split = dl.Split("@");
                    var version = split.Length > 1 ? split[1] : null;
                    dl = split.Length > 1 ? split[0] : dl;

                    var component = new VcpkgComponent(item.SPDXID, item.Name, version, downloadLocation: dl);
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                }
            }
            catch (Exception e)
            {
                this.Logger.LogWarning(e, "failed while handling {ItemName}", item.Name);
                singleFileComponentRecorder.RegisterPackageParseFailure(item.Name);
            }
        }
    }

    /// <summary>
    /// Attempts to resolve and return a manifest component recorder for the given recorder.
    /// Returns the matching manifest component recorder if found; otherwise, returns the original recorder.
    /// </summary>
    private ISingleFileComponentRecorder GetManifestComponentRecorder(ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        try
        {
            const string vcpkgInstalled = "vcpkg_installed";
            var manifestFileLocation = singleFileComponentRecorder.ManifestFileLocation;

            var vcpkgInstalledIndex = manifestFileLocation.IndexOf(vcpkgInstalled, StringComparison.OrdinalIgnoreCase);
            if (vcpkgInstalledIndex < 0)
            {
                this.Logger.LogWarning(
                    "Could not find '{VcpkgInstalled}' in ManifestFileLocation: '{ManifestFileLocation}'. Returning original recorder.",
                    vcpkgInstalled,
                    manifestFileLocation);

                return singleFileComponentRecorder;
            }

            var vcpkgInstalledDir = manifestFileLocation[..(vcpkgInstalledIndex + vcpkgInstalled.Length)];

            var preferredManifest = Path.Combine(vcpkgInstalledDir, "vcpkg", "manifest-info.json");
            var fallbackManifest = Path.Combine(vcpkgInstalledDir, "manifest-info.json");

            // Try preferred location first
            if (this.manifestMappings.TryGetValue(preferredManifest, out var manifestData) && manifestData != null)
            {
                return this.ComponentRecorder.CreateSingleFileComponentRecorder(manifestData.ManifestPath);
            }
            else if (this.manifestMappings.TryGetValue(fallbackManifest, out manifestData) && manifestData != null)
            {
                // Use the fallback location.
                this.Logger.LogWarning(
                    "Preferred manifest at '{PreferredManifest}' was not found or invalid. Using fallback manifest at '{FallbackManifest}'.",
                    preferredManifest,
                    fallbackManifest);

                return this.ComponentRecorder.CreateSingleFileComponentRecorder(manifestData.ManifestPath);
            }
            else
            {
                this.Logger.LogWarning(
                    "No valid manifest-info.json found at either '{PreferredManifest}' or '{FallbackManifest}' for base location '{VcpkgInstalledDir}'. Returning original recorder.",
                    preferredManifest,
                    fallbackManifest,
                    vcpkgInstalledDir);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(
                ex,
                "An exception occurred while resolving manifest component recorder for '{ManifestFileLocation}'. Returning original recorder.",
                singleFileComponentRecorder.ManifestFileLocation);
        }

        // Always return the original recorder if no manifest is found or on error
        return singleFileComponentRecorder;
    }
}

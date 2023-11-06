namespace Microsoft.ComponentDetection.Detectors.Vcpkg;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class VcpkgComponentDetector : FileComponentDetector, IExperimentalDetector
{
    private readonly HashSet<string> projectRoots = new HashSet<string>();

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

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Vcpkg) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "vcpkg.spdx.json" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Vcpkg };

    public override int Version => 2;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogDebug("vcpkg detector found {File}", file);

        var projectRootDirectory = Directory.GetParent(file.Location);
        if (this.projectRoots.Any(path => projectRootDirectory.FullName.StartsWith(path)))
        {
            return;
        }

        await this.ParseSpdxFileAsync(singleFileComponentRecorder, file);
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
}

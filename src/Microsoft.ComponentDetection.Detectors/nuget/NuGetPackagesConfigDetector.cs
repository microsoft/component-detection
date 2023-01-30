namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using System.Xml;
using global::NuGet.Packaging;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[Export(typeof(IComponentDetector))]
public class NuGetPackagesConfigDetector : FileComponentDetector
{
    public override IList<string> SearchPatterns => new[] { "packages.config" };

    public override string Id => "NuGetPackagesConfig";

    public override IEnumerable<string> Categories =>
        new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet) };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.NuGet };

    public override int Version => 1;

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        try
        {
            var packagesConfig = new PackagesConfigReader(processRequest.ComponentStream.Stream);
            foreach (var package in packagesConfig.GetPackages(allowDuplicatePackageIds: true))
            {
                processRequest.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new NuGetComponent(
                            package.PackageIdentity.Id,
                            package.PackageIdentity.Version.ToNormalizedString())),
                    true,
                    null,
                    package.IsDevelopmentDependency);
            }
        }
        catch (Exception e) when (e is PackagesConfigReaderException or XmlException)
        {
            this.Logger.LogFailedReadingFile(processRequest.ComponentStream.Location, e);
        }

        return Task.CompletedTask;
    }
}

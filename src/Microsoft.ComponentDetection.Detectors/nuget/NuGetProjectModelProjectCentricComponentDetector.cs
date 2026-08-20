#nullable disable
namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.ProjectModel;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class NuGetProjectModelProjectCentricComponentDetector : FileComponentDetector
{
    private readonly IFileUtilityService fileUtilityService;

    public NuGetProjectModelProjectCentricComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IFileUtilityService fileUtilityService,
        ILogger<NuGetProjectModelProjectCentricComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    public override string Id { get; } = "NuGetProjectCentric";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)];

    public override IList<string> SearchPatterns { get; } = ["project.assets.json"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.NuGet];

    public override int Version { get; } = 2;

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockFile = new LockFileFormat().Read(processRequest.ComponentStream.Stream, processRequest.ComponentStream.Location);

            this.RecordLockfileVersion(lockFile.Version);

            if (lockFile.PackageSpec == null)
            {
                this.Logger.LogWarning("Lock file {LockFilePath} does not contain a PackageSpec.", processRequest.ComponentStream.Location);
                return Task.CompletedTask;
            }

            // Since we report projects as the location, we ignore the passed in single file recorder.
            var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(lockFile.PackageSpec.RestoreMetadata.ProjectPath);
            LockFileUtilities.ProcessLockFile(lockFile, singleFileComponentRecorder, this.Logger);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the package
            this.Logger.LogError(e, "Failed to process NuGet lockfile {NuGetLockFile}", processRequest.ComponentStream.Location);
        }

        return Task.CompletedTask;
    }
}

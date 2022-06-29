using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Nett;

namespace Microsoft.ComponentDetection.Detectors.Rust
{
    [Export(typeof(IComponentDetector))]
    public class RustCrateDetector : FileComponentDetector
    {
        public override string Id => "RustCrateDetector";

        public override IList<string> SearchPatterns => new List<string> { RustCrateUtilities.CargoLockSearchPattern };

        public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Cargo };

        public override int Version { get; } = 7;

        public override IEnumerable<string> Categories => new List<string> { "Rust" };

        protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var cargoLockFile = processRequest.ComponentStream;

            try
            {
                var cargoLock = StreamTomlSerializer.Deserialize(cargoLockFile.Stream, TomlSettings.Create()).Get<CargoLock>();

                // This makes sure we're only trying to parse Cargo.lock v1 formats
                if (cargoLock.metadata == null)
                {
                    Logger.LogInfo($"Cargo.lock file at {cargoLockFile.Location} contains no metadata section so we're parsing it as the v2 format. The v1 detector will not process it.");
                    return Task.CompletedTask;
                }

                FileInfo lockFileInfo = new FileInfo(cargoLockFile.Location);
                IEnumerable<IComponentStream> cargoTomlComponentStream = ComponentStreamEnumerableFactory.GetComponentStreams(lockFileInfo.Directory, new List<string> { RustCrateUtilities.CargoTomlSearchPattern }, (name, directoryName) => false, recursivelyScanDirectories: false);

                CargoDependencyData cargoDependencyData = RustCrateUtilities.ExtractRootDependencyAndWorkspaceSpecifications(cargoTomlComponentStream, singleFileComponentRecorder);

                // If workspaces have been defined in the root cargo.toml file, scan for specified cargo.toml manifests
                int numWorkspaceComponentStreams = 0;
                int expectedWorkspaceTomlCount = cargoDependencyData.CargoWorkspaces.Count;
                if (expectedWorkspaceTomlCount > 0)
                {
                    string rootCargoTomlLocation = Path.Combine(lockFileInfo.DirectoryName, "Cargo.toml");

                    IEnumerable<IComponentStream> cargoTomlWorkspaceComponentStreams = ComponentStreamEnumerableFactory.GetComponentStreams(
                        lockFileInfo.Directory,
                        new List<string> { RustCrateUtilities.CargoTomlSearchPattern },
                        RustCrateUtilities.BuildExcludeDirectoryPredicateFromWorkspaces(lockFileInfo, cargoDependencyData.CargoWorkspaces, cargoDependencyData.CargoWorkspaceExclusions),
                        recursivelyScanDirectories: true)
                        .Where(x => !x.Location.Equals(rootCargoTomlLocation)); // The root directory needs to be included in directoriesToScan, but should not be reprocessed
                    numWorkspaceComponentStreams = cargoTomlWorkspaceComponentStreams.Count();

                    // Now that the non-root files have been located, add their dependencies
                    RustCrateUtilities.ExtractDependencySpecifications(cargoTomlWorkspaceComponentStreams, singleFileComponentRecorder, cargoDependencyData.NonDevDependencies, cargoDependencyData.DevDependencies);
                }

                // Even though we can't read the file streams, we still have the enumerable!
                if (!cargoTomlComponentStream.Any() || cargoTomlComponentStream.Count() > 1)
                {
                    Logger.LogWarning($"We are expecting exactly 1 accompanying Cargo.toml file next to the cargo.lock file found at {cargoLockFile.Location}");
                    return Task.CompletedTask;
                }

                // If there is a mismatch between the number of expected and found workspaces, exit
                if (expectedWorkspaceTomlCount > numWorkspaceComponentStreams)
                {
                    Logger.LogWarning($"We are expecting at least {expectedWorkspaceTomlCount} accompanying Cargo.toml file(s) from workspaces outside of the root directory {lockFileInfo.DirectoryName}, but found {numWorkspaceComponentStreams}");
                    return Task.CompletedTask;
                }

                var cargoPackages = cargoLock.package.ToHashSet();
                RustCrateUtilities.BuildGraph(cargoPackages, cargoDependencyData.NonDevDependencies, cargoDependencyData.DevDependencies, singleFileComponentRecorder);
            }
            catch (Exception e)
            {
                // If something went wrong, just ignore the file
                Logger.LogFailedReadingFile(cargoLockFile.Location, e);
            }

            return Task.CompletedTask;
        }
    }
}

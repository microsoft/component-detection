using System.Composition;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common;

[Export(typeof(IDetectorDependencies))]
public class DetectorDependencies : IDetectorDependencies
{
    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IComponentStreamEnumerableFactory ComponentStreamEnumerableFactory { get; set; }

    [Import]
    public IPathUtilityService PathUtilityService { get; set; }

    [Import]
    public ICommandLineInvocationService CommandLineInvocationService { get; set; }

    [Import]
    public IFileUtilityService FileUtilityService { get; set; }

    [Import]
    public IObservableDirectoryWalkerFactory DirectoryWalkerFactory { get; set; }

    [Import]
    public IDockerService DockerService { get; set; }

    [Import]
    public IEnvironmentVariableService EnvironmentVariableService { get; set; }
}

namespace Microsoft.ComponentDetection.Common;
using System.Composition;
using Microsoft.ComponentDetection.Contracts;

[Export(typeof(IDetectorDependencies))]
public class DetectorDependencies : IDetectorDependencies
{
    public DetectorDependencies()
    {
    }

    public DetectorDependencies(
            IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
            IPathUtilityService pathUtilityService,
            ICommandLineInvocationService commandLineInvocationService,
            IFileUtilityService fileUtilityService,
            IObservableDirectoryWalkerFactory directoryWalkerFactory,
            IDockerService dockerService,
            IEnvironmentVariableService environmentVariableService,
            ILogger logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.PathUtilityService = pathUtilityService;
        this.CommandLineInvocationService = commandLineInvocationService;
        this.FileUtilityService = fileUtilityService;
        this.DirectoryWalkerFactory = directoryWalkerFactory;
        this.DockerService = dockerService;
        this.EnvironmentVariableService = environmentVariableService;
        this.Logger = logger;
    }

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

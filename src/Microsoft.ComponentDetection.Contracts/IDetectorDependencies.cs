namespace Microsoft.ComponentDetection.Contracts
{
    public interface IDetectorDependencies
    {
        ILogger Logger { get; set; }

        IComponentStreamEnumerableFactory ComponentStreamEnumerableFactory { get; set; }

        IPathUtilityService PathUtilityService { get; set; }

        ICommandLineInvocationService CommandLineInvocationService { get; set; }

        IFileUtilityService FileUtilityService { get; set; }

        IObservableDirectoryWalkerFactory DirectoryWalkerFactory { get; set; }
        
        IDockerService DockerService { get; set; }

        IEnvironmentVariableService EnvironmentVariableService { get; set; }
    }
}

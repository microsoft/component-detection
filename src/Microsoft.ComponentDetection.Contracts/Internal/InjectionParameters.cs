using System.Composition;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Orchestrator, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b101e980bad6a4194bcaf85cf037aecbe8b1fca61429ad511862c91be758742390c40ecc64c3a664103b071f6b3a563dd18c460c98f74a4fe2eaca8ca2672c777aec1a2d4672e3e4c0fb005548fe4a39c9fa48c8b6d094444dc45b02c4f9bf1fa7b3b91cdbe4921717869973a8d96f4f3a371f22ed03ff9b700f1534c014d5cb")]

namespace Microsoft.ComponentDetection.Contracts.Internal
{
    // Why do we have this weird garbage code?
    // Because https://github.com/dotnet/corefx/issues/11856
    // This ugly file is here mostly because we don't have a way to easily expose instances to our composed detectors (NetStandard MEF hasn't received the love).
    // It needs to live in the Contracts project to isolate part discovery to a different assembly.
    // It also makes a little bit of sense because everything that IComponentDetectors can inject is in this file :).
    internal class InjectionParameters
    {
        public InjectionParameters()
        {
        }

        internal InjectionParameters(IDetectorDependencies detectorDependencies)
        {
            loggerStatic = detectorDependencies.Logger;
            factoryStatic = detectorDependencies.ComponentStreamEnumerableFactory;
            pathUtilityServiceStatic = detectorDependencies.PathUtilityService;
            commandLineInvocationServiceStatic = detectorDependencies.CommandLineInvocationService;
            fileUtilityServiceStatic = detectorDependencies.FileUtilityService;
            observableDirectoryWalkerFactoryServiceStatic = detectorDependencies.DirectoryWalkerFactory;
            dockerServiceStatic = detectorDependencies.DockerService;
            environmentVariableServiceStatic = detectorDependencies.EnvironmentVariableService;
        }

        private static ILogger loggerStatic;
        private static IComponentStreamEnumerableFactory factoryStatic;
        private static IPathUtilityService pathUtilityServiceStatic;
        private static ICommandLineInvocationService commandLineInvocationServiceStatic;
        private static IFileUtilityService fileUtilityServiceStatic;
        private static IObservableDirectoryWalkerFactory observableDirectoryWalkerFactoryServiceStatic;
        private static IDockerService dockerServiceStatic;

        private static IEnvironmentVariableService environmentVariableServiceStatic;

        [Export(typeof(ILogger))]
        public ILogger Logger => loggerStatic;

        [Export(typeof(IComponentStreamEnumerableFactory))]
        public IComponentStreamEnumerableFactory Factory => factoryStatic;

        [Export(typeof(IPathUtilityService))]
        public IPathUtilityService PathUtilityService => pathUtilityServiceStatic;

        [Export(typeof(ICommandLineInvocationService))]
        public ICommandLineInvocationService CommandLineInvocationService => commandLineInvocationServiceStatic;

        [Export(typeof(IFileUtilityService))]
        public IFileUtilityService FileUtilityService => fileUtilityServiceStatic;

        [Export(typeof(IObservableDirectoryWalkerFactory))]
        public IObservableDirectoryWalkerFactory ObservableDirectoryWalkerFactory => observableDirectoryWalkerFactoryServiceStatic;

        [Export(typeof(IDockerService))]
        public IDockerService DockerService => dockerServiceStatic;

        [Export(typeof(IEnvironmentVariableService))]
        public IEnvironmentVariableService EnvironmentVariableService => environmentVariableServiceStatic;
    }
}

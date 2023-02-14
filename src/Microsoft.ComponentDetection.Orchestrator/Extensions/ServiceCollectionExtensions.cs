namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.CocoaPods;
using Microsoft.ComponentDetection.Detectors.Dockerfile;
using Microsoft.ComponentDetection.Detectors.Go;
using Microsoft.ComponentDetection.Detectors.Gradle;
using Microsoft.ComponentDetection.Detectors.Ivy;
using Microsoft.ComponentDetection.Detectors.Linux;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Detectors.Pnpm;
using Microsoft.ComponentDetection.Detectors.Poetry;
using Microsoft.ComponentDetection.Detectors.Ruby;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Spdx;
using Microsoft.ComponentDetection.Detectors.Vcpkg;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Component Detection services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to register the services with.</param>
    /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
    public static IServiceCollection AddComponentDetection(this IServiceCollection services)
    {
        services.AddSingleton<Orchestrator>();

        // Shared services
        services.AddSingleton<ITelemetryService, CommandLineTelemetryService>();
        services.AddSingleton<ICommandLineInvocationService, CommandLineInvocationService>();
        services.AddSingleton<IComponentStreamEnumerableFactory, ComponentStreamEnumerableFactory>();
        services.AddSingleton<IConsoleWritingService, ConsoleWritingService>();
        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IEnvironmentVariableService, EnvironmentVariableService>();
        services.AddSingleton<IObservableDirectoryWalkerFactory, FastDirectoryWalkerFactory>();
        services.AddSingleton<IFileUtilityService, FileUtilityService>();
        services.AddSingleton<IFileWritingService, FileWritingService>();
        services.AddSingleton<IGraphTranslationService, DefaultGraphTranslationService>();
        services.AddSingleton<ILogger, Logger>();
        services.AddSingleton<IPathUtilityService, PathUtilityService>();
        services.AddSingleton<ISafeFileEnumerableFactory, SafeFileEnumerableFactory>();

        // Command line services
        services.AddSingleton<IScanArguments, BcdeArguments>();
        services.AddSingleton<IScanArguments, BcdeDevArguments>();
        services.AddSingleton<IScanArguments, ListDetectionArgs>();
        services.AddSingleton<IArgumentHandlingService, BcdeDevCommandService>();
        services.AddSingleton<IArgumentHandlingService, BcdeScanCommandService>();
        services.AddSingleton<IArgumentHandlingService, DetectorListingCommandService>();
        services.AddSingleton<IBcdeScanExecutionService, BcdeScanExecutionService>();
        services.AddSingleton<IDetectorProcessingService, DetectorProcessingService>();
        services.AddSingleton<IDetectorRestrictionService, DetectorRestrictionService>();
        services.AddSingleton<IArgumentHelper, ArgumentHelper>();

        // Detectors
        // CocoaPods
        services.AddSingleton<IComponentDetector, PodComponentDetector>();

        // Dockerfile
        services.AddSingleton<IComponentDetector, DockerfileComponentDetector>();

        // Go
        services.AddSingleton<IComponentDetector, GoComponentDetector>();

        // Gradle
        services.AddSingleton<IComponentDetector, GradleComponentDetector>();

        // Ivy
        services.AddSingleton<IComponentDetector, IvyDetector>();

        // Linux
        services.AddSingleton<ILinuxScanner, LinuxScanner>();
        services.AddSingleton<IComponentDetector, LinuxContainerDetector>();

        // Maven
        services.AddSingleton<IMavenCommandService, MavenCommandService>();
        services.AddSingleton<IMavenStyleDependencyGraphParserService, MavenStyleDependencyGraphParserService>();
        services.AddSingleton<IComponentDetector, MvnCliComponentDetector>();

        // npm
        services.AddSingleton<IComponentDetector, NpmComponentDetector>();
        services.AddSingleton<IComponentDetector, NpmComponentDetectorWithRoots>();

        // NuGet
        services.AddSingleton<IComponentDetector, NuGetComponentDetector>();
        services.AddSingleton<IComponentDetector, NuGetPackagesConfigDetector>();
        services.AddSingleton<IComponentDetector, NuGetProjectModelProjectCentricComponentDetector>();

        // PIP
        services.AddSingleton<IPyPiClient, PyPiClient>();
        services.AddSingleton<IPythonCommandService, PythonCommandService>();
        services.AddSingleton<IPythonResolver, PythonResolver>();
        services.AddSingleton<IComponentDetector, PipComponentDetector>();

        // pnpm
        services.AddSingleton<IComponentDetector, PnpmComponentDetector>();

        // Poetry
        services.AddSingleton<IComponentDetector, PoetryComponentDetector>();

        // Ruby
        services.AddSingleton<IComponentDetector, RubyComponentDetector>();

        // Rust
        services.AddSingleton<IComponentDetector, RustCrateDetector>();

        // SPDX
        services.AddSingleton<IComponentDetector, Spdx22ComponentDetector>();

        // VCPKG
        services.AddSingleton<IComponentDetector, VcpkgComponentDetector>();

        // Yarn
        services.AddSingleton<IYarnLockParser, YarnLockParser>();
        services.AddSingleton<IYarnLockFileFactory, YarnLockFileFactory>();
        services.AddSingleton<IComponentDetector, YarnLockComponentDetector>();

        return services;
    }
}

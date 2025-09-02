namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.CocoaPods;
using Microsoft.ComponentDetection.Detectors.Conan;
using Microsoft.ComponentDetection.Detectors.Dockerfile;
using Microsoft.ComponentDetection.Detectors.DotNet;
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
using Microsoft.ComponentDetection.Detectors.Swift;
using Microsoft.ComponentDetection.Detectors.Uv;
using Microsoft.ComponentDetection.Detectors.Vcpkg;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.ComponentDetection.Orchestrator.Experiments;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
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
        // Shared services
        services.AddSingleton<ITelemetryService, CommandLineTelemetryService>();
        services.AddSingleton<ICommandLineInvocationService, CommandLineInvocationService>();
        services.AddSingleton<IComponentStreamEnumerableFactory, ComponentStreamEnumerableFactory>();
        services.AddSingleton<IConsoleWritingService, ConsoleWritingService>();
        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IEnvironmentVariableService, EnvironmentVariableService>();
        services.AddSingleton<IObservableDirectoryWalkerFactory, FastDirectoryWalkerFactory>();
        services.AddSingleton<IFileUtilityService, FileUtilityService>();
        services.AddSingleton<IDirectoryUtilityService, DirectoryUtilityService>();
        services.AddSingleton<IFileWritingService, FileWritingService>();
        services.AddSingleton<IGraphTranslationService, DefaultGraphTranslationService>();
        services.AddSingleton<IPathUtilityService, PathUtilityService>();
        services.AddSingleton<ISafeFileEnumerableFactory, SafeFileEnumerableFactory>();

        // Command line services
        services.AddSingleton<IScanExecutionService, ScanExecutionService>();
        services.AddSingleton<IDetectorProcessingService, DetectorProcessingService>();
        services.AddSingleton<IDetectorRestrictionService, DetectorRestrictionService>();
        services.AddSingleton<IArgumentHelper, ArgumentHelper>();

        // Experiments
        services.AddSingleton<IExperimentService, ExperimentService>();
        services.AddSingleton<IExperimentProcessor, DefaultExperimentProcessor>();
        services.AddSingleton<IExperimentConfiguration, SimplePipExperiment>();
        services.AddSingleton<IExperimentConfiguration, RustSbomVsCliExperiment>();
        services.AddSingleton<IExperimentConfiguration, RustSbomVsCrateExperiment>();
        services.AddSingleton<IExperimentConfiguration, UvLockDetectorExperiment>();

        // Detectors
        // CocoaPods
        services.AddSingleton<IComponentDetector, PodComponentDetector>();

        // Conan
        services.AddSingleton<IComponentDetector, ConanLockComponentDetector>();

        // Conda
        services.AddSingleton<IComponentDetector, CondaLockComponentDetector>();

        // Dockerfile
        services.AddSingleton<IComponentDetector, DockerfileComponentDetector>();

        // DotNet
        services.AddSingleton<IComponentDetector, DotNetComponentDetector>();

        // Go
        services.AddSingleton<IComponentDetector, GoComponentDetector>();
        services.AddSingleton<IGoParserFactory, GoParserFactory>();

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
        services.AddSingleton<IComponentDetector, NpmLockfile3Detector>();

        // NuGet
        services.AddSingleton<IComponentDetector, NuGetComponentDetector>();
        services.AddSingleton<IComponentDetector, NuGetPackagesConfigDetector>();
        services.AddSingleton<IComponentDetector, NuGetProjectModelProjectCentricComponentDetector>();

        // PIP
        services.AddSingleton<IPyPiClient, PyPiClient>();
        services.AddSingleton<ISimplePyPiClient, SimplePyPiClient>();
        services.AddSingleton<IPythonCommandService, PythonCommandService>();
        services.AddSingleton<IPythonResolver, PythonResolver>();
        services.AddSingleton<ISimplePythonResolver, SimplePythonResolver>();
        services.AddSingleton<IComponentDetector, PipComponentDetector>();
        services.AddSingleton<IComponentDetector, SimplePipComponentDetector>();
        services.AddSingleton<IPipCommandService, PipCommandService>();
        services.AddSingleton<IComponentDetector, PipReportComponentDetector>();

        // pnpm
        services.AddSingleton<IComponentDetector, PnpmComponentDetectorFactory>();

        // Poetry
        services.AddSingleton<IComponentDetector, PoetryComponentDetector>();

        // Ruby
        services.AddSingleton<IComponentDetector, RubyComponentDetector>();

        // Rust
        services.AddSingleton<IComponentDetector, RustCrateDetector>();
        services.AddSingleton<IComponentDetector, RustCliDetector>();
        services.AddSingleton<IComponentDetector, RustSbomDetector>();

        // SPDX
        services.AddSingleton<IComponentDetector, Spdx22ComponentDetector>();

        // VCPKG
        services.AddSingleton<IComponentDetector, VcpkgComponentDetector>();

        // Yarn
        services.AddSingleton<IYarnLockParser, YarnLockParser>();
        services.AddSingleton<IYarnLockFileFactory, YarnLockFileFactory>();
        services.AddSingleton<IComponentDetector, YarnLockComponentDetector>();

        // Swift Package Manager
        services.AddSingleton<IComponentDetector, SwiftResolvedComponentDetector>();

        // uv
        services.AddSingleton<IComponentDetector, UvLockComponentDetector>();

        return services;
    }
}

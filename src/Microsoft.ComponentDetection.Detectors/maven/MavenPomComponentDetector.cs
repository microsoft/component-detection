namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class MavenPomComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private readonly IMavenFileParserService mavenFileParserService;

    public MavenPomComponentDetector(
     IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
     IObservableDirectoryWalkerFactory walkerFactory,
     IMavenFileParserService mavenFileParserService,
     ILogger<MavenPomComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.mavenFileParserService = mavenFileParserService;
        this.Logger = logger;
    }

    public override string Id => "MvnPom";

    public override IList<string> SearchPatterns => new List<string>() { "*.pom" };

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Maven) };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Maven };

    public override int Version => 2;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        await this.ProcessFileAsync(processRequest);
    }

    private async Task ProcessFileAsync(ProcessRequest processRequest)
    {
        this.mavenFileParserService.ParseDependenciesFile(processRequest);

        await Task.CompletedTask;
    }
}

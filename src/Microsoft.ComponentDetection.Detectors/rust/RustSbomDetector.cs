namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class RustSbomDetector : FileComponentDetector, IExperimentalDetector
{
    private const string CargoSbomSearchPattern = "*.cargo-sbom.json";
    private readonly RustSbomParser parser;

    public RustSbomDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<RustSbomDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
        this.parser = new RustSbomParser(logger);
    }

    public override string Id => "RustSbom";

    public override IList<string> SearchPatterns => [CargoSbomSearchPattern];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Cargo];

    public override int Version { get; } = 1;

    public override IEnumerable<string> Categories => ["Rust"];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var components = processRequest.ComponentStream;
        var sbomVersion = await this.parser.ParseAsync(components, singleFileComponentRecorder, cancellationToken);
        if (sbomVersion.HasValue)
        {
            this.RecordLockfileVersion(sbomVersion.Value);
        }
    }
}

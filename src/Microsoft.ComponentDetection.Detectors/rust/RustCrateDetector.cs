namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class RustCrateDetector : FileComponentDetector
{
    private const string CargoLockSearchPattern = "Cargo.lock";
    private readonly IRustCargoLockParser parser;

    public RustCrateDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<RustCrateDetector> logger,
        IRustCargoLockParser parser)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
        this.parser = parser;
    }

    public override string Id => "RustCrateDetector";

    public override IList<string> SearchPatterns => [CargoLockSearchPattern];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Cargo];

    public override int Version { get; } = 9;

    public override IEnumerable<string> Categories => ["Rust"];

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var cargoLockFile = processRequest.ComponentStream;

        var lockfileVersion = await this.parser.ParseAsync(cargoLockFile, singleFileComponentRecorder, cancellationToken);

        if (lockfileVersion.HasValue)
        {
            this.RecordLockfileVersion(lockfileVersion.Value);
        }
    }
}

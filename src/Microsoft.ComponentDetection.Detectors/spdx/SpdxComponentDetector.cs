namespace Microsoft.ComponentDetection.Detectors.Spdx;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public abstract class SpdxComponentDetector : FileComponentDetector
{
    protected SpdxComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override IEnumerable<string> Categories => [nameof(DetectorClass.Spdx)];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Spdx];

    public override IList<string> SearchPatterns => ["*.spdx.json"];

    protected abstract SpdxComponent? ReadDocument(JsonElement root, string checksum, string path);

    protected async Task ProcessFileAsync(ProcessRequest processRequest, CancellationToken cancellationToken)
    {
        var file = processRequest.ComponentStream;
        using var stream = file.Stream;

#pragma warning disable CA5350, CA1308 // Existing SPDX document references require lowercase SHA1 checksums.
        var checksum = Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
#pragma warning restore CA5350, CA1308
        stream.Seek(0, SeekOrigin.Begin);

        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var component = this.ReadDocument(json.RootElement, checksum, file.Location);
        if (component != null)
        {
            processRequest.SingleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
        }
    }
}

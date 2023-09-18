namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>Specialized base class for file based component detection.</summary>
public abstract class FileComponentDetector : IComponentDetector
{
    /// <summary>
    /// Gets or sets the factory for handing back component streams to File detectors.
    /// </summary>
    protected IComponentStreamEnumerableFactory ComponentStreamEnumerableFactory { get; set; }

    protected IDirectoryWalkerFactory Scanner { get; set; }

    /// <summary>
    /// Gets or sets the logger for writing basic logging message to both console and file.
    /// </summary>
    protected ILogger Logger { get; set; }

    public IComponentRecorder ComponentRecorder { get; private set; }

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <summary> Gets the search patterns used to produce the list of valid folders to scan. These patterns are evaluated with .Net's Directory.EnumerateFiles function. </summary>
    public abstract IList<string> SearchPatterns { get; }

    /// <summary>Gets the categories this detector is considered a member of. Used by the DetectorCategories arg to include detectors.</summary>
    public abstract IEnumerable<string> Categories { get; }

    /// <summary>Gets the supported component types. </summary>
    public abstract IEnumerable<ComponentType> SupportedComponentTypes { get; }

    /// <summary>Gets the version of this component detector. </summary>
    public abstract int Version { get; }

    /// <summary>
    /// Gets the folder names that will be skipped by the Component Detector.
    /// </summary>
    protected virtual IList<string> SkippedFolders => new List<string> { };

    /// <summary>
    /// Gets or sets the active scan request -- only populated after a ScanDirectoryAsync is invoked. If ScanDirectoryAsync is overridden,
    ///  the overrider should ensure this property is populated.
    /// </summary>
    protected ScanRequest CurrentScanRequest { get; set; }

    public bool NeedsAutomaticRootDependencyCalculation { get; protected set; }

    protected Dictionary<string, string> Telemetry { get; set; } = new Dictionary<string, string>();

    protected IObservable<IComponentStream> ComponentStreams { get; private set; }

    /// <inheritdoc />
    public async virtual Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request)
    {
        this.CurrentScanRequest = request;
        this.ComponentRecorder = request.ComponentRecorder;

        this.Logger.LogDebug("Registered {Detector}", this.GetType().FullName);

        var requests = new ConcurrentBag<ProcessRequest>();

        await this.Scanner.WalkDirectoryAsync(
            request.SourceDirectory,
            request.DirectoryExclusionPredicate,
            request.ComponentRecorder,
            async (processRequest) =>
            {
                requests.Add(processRequest);
                await Task.CompletedTask;
            },
            this.SearchPatterns);

        var filteredRequests = await this.OnPrepareDetectionAsync(requests, request.DetectorArgs);

        var actionBlock = new ActionBlock<ProcessRequest>(async pr => await this.OnFileFoundAsync(pr, request.DetectorArgs));

        foreach (var processRequest in filteredRequests)
        {
            await actionBlock.SendAsync(processRequest);
        }

        actionBlock.Complete();
        await actionBlock.Completion;

        await this.OnDetectionFinishedAsync();

        return new IndividualDetectorScanResult
        {
            ResultCode = ProcessingResultCode.Success,
            AdditionalTelemetryDetails = this.Telemetry,
        };
    }

    /// <summary>
    /// Records the lockfile version in the telemetry.
    /// </summary>
    /// <param name="lockfileVersion">The lockfile version.</param>
    protected void RecordLockfileVersion(int lockfileVersion) => this.RecordLockfileVersion(lockfileVersion.ToString());

    /// <summary>
    /// Records the lockfile version in the telemetry.
    /// </summary>
    /// <param name="lockfileVersion">The lockfile version.</param>
    protected void RecordLockfileVersion(string lockfileVersion) => this.Telemetry["LockfileVersion"] = lockfileVersion;

    protected virtual Task<IEnumerable<ProcessRequest>> OnPrepareDetectionAsync(IEnumerable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
    {
        return Task.FromResult(processRequests);
    }

    protected abstract Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs);

    protected virtual Task OnDetectionFinishedAsync()
    {
        return Task.CompletedTask;
    }
}

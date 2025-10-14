#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
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

    protected IObservableDirectoryWalkerFactory Scanner { get; set; }

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
    protected virtual IList<string> SkippedFolders => [];

    /// <summary>
    /// Gets or sets the active scan request -- only populated after a ScanDirectoryAsync is invoked. If ScanDirectoryAsync is overridden,
    ///  the overrider should ensure this property is populated.
    /// </summary>
    protected ScanRequest CurrentScanRequest { get; set; }

    public virtual bool NeedsAutomaticRootDependencyCalculation { get; protected set; }

    protected ConcurrentDictionary<string, string> Telemetry { get; set; } = [];

    /// <summary>
    /// List of any any additional properties as key-value pairs that we would like to capture for the detector.
    /// </summary>
    public List<(string PropertyKey, string PropertyValue)> AdditionalProperties { get; set; } = [];

    protected IObservable<IComponentStream> ComponentStreams { get; private set; }

    protected virtual bool EnableParallelism { get; set; }

    /// <inheritdoc />
    public async virtual Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        this.ComponentRecorder = request.ComponentRecorder;
        this.Scanner.Initialize(request.SourceDirectory, request.DirectoryExclusionPredicate, 1);
        return await this.ScanDirectoryAsync(request, cancellationToken);
    }

    private Task<IndividualDetectorScanResult> ScanDirectoryAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        this.CurrentScanRequest = request;

        var filteredObservable = this.Scanner.GetFilteredComponentStreamObservable(request.SourceDirectory, this.SearchPatterns, request.ComponentRecorder);

        this.Logger.LogDebug("Registered {Detector}", this.GetType().FullName);
        return this.ProcessAsync(filteredObservable, request.DetectorArgs, request.MaxThreads, request.CleanupCreatedFiles, cancellationToken);
    }

    /// <summary>
    /// Gets the file streams for the Detector's declared <see cref="SearchPatterns"/> as an <see cref="IEnumerable{IComponentStream}"/>.
    /// </summary>
    /// <param name="sourceDirectory">The directory to search.</param>
    /// <param name="exclusionPredicate">The exclusion predicate function.</param>
    /// <returns>Awaitable task with enumerable streams <see cref="IEnumerable{IComponentStream}"/> for the declared detector. </returns>
    protected Task<IEnumerable<IComponentStream>> GetFileStreamsAsync(DirectoryInfo sourceDirectory, ExcludeDirectoryPredicate exclusionPredicate)
    {
        return Task.FromResult(this.ComponentStreamEnumerableFactory.GetComponentStreams(sourceDirectory, this.SearchPatterns, exclusionPredicate));
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

    private async Task<IndividualDetectorScanResult> ProcessAsync(
        IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs, int maxThreads, bool cleanupCreatedFiles, CancellationToken cancellationToken = default)
    {
        var threadsToUse = this.EnableParallelism ? Math.Min(Environment.ProcessorCount, maxThreads) : 1;
        this.Telemetry["ThreadsUsed"] = $"{threadsToUse}";

        var processor = new ActionBlock<ProcessRequest>(
            async processRequest => await this.OnFileFoundAsync(processRequest, detectorArgs, cleanupCreatedFiles, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                // MaxDegreeOfParallelism is the lower of the processor count and the max threads arg that the customer passed in
                MaxDegreeOfParallelism = threadsToUse,
            });

        var preprocessedObserbable = await this.OnPrepareDetectionAsync(processRequests, detectorArgs, cancellationToken);

        await preprocessedObserbable.ForEachAsync(processRequest => processor.Post(processRequest));

        processor.Complete();

        await processor.Completion;

        await this.OnDetectionFinishedAsync();

        return new IndividualDetectorScanResult
        {
            ResultCode = ProcessingResultCode.Success,
            AdditionalTelemetryDetails = this.Telemetry.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
    }

    /// <summary>
    /// Auxliary method executed before the actual scanning of a given file takes place.
    /// This method can be used to modify or create new ProcessRequests that later will
    /// be used by the scanner to extract the components.
    /// </summary>
    /// <param name="processRequests">Process requests that triggered a given scanner.</param>
    /// <param name="detectorArgs">Arguments used by the detector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Returns the process requests that will be used by the scanner.</returns>
    protected virtual Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(processRequests);
    }

    /// <summary>
    /// This method called for each individual process request.
    /// It is responsible for the actual scanning of the components.
    /// </summary>
    /// <param name="processRequest">Process request that contains information to execute the component scan.</param>
    /// <param name="detectorArgs">Arguments for the detector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected abstract Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default);

    protected virtual Task OnDetectionFinishedAsync()
    {
        return Task.CompletedTask;
    }

    // Do not cleanup by default, only if the detector uses the FileComponentWithCleanup abstract class.
    protected virtual async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, bool cleanupCreatedFiles, CancellationToken cancellationToken = default) =>
        await this.OnFileFoundAsync(processRequest, detectorArgs, cancellationToken);
}

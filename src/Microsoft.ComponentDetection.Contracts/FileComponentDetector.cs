namespace Microsoft.ComponentDetection.Contracts;
using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>Specialized base class for file based component detection.</summary>
public abstract class FileComponentDetector : IComponentDetector
{
    /// <summary>
    /// Gets or sets the factory for handing back component streams to File detectors. Injected automatically by MEF composition.
    /// </summary>
    [Import]
    public IComponentStreamEnumerableFactory ComponentStreamEnumerableFactory { get; set; }

    /// <summary>Gets or sets the logger for writing basic logging message to both console and file. Injected automatically by MEF composition.</summary>
    [Import]
    public ILogger Logger { get; set; }

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

    [Import]
    public IObservableDirectoryWalkerFactory Scanner { get; set; }

    public bool NeedsAutomaticRootDependencyCalculation { get; protected set; }

    protected Dictionary<string, string> Telemetry { get; set; } = new Dictionary<string, string>();

    protected IObservable<IComponentStream> ComponentStreams { get; private set; }

    /// <inheritdoc />
    public async virtual Task<IndividualDetectorScanResult> ExecuteDetectorAsync(ScanRequest request)
    {
        this.ComponentRecorder = request.ComponentRecorder;
        this.Scanner.Initialize(request.SourceDirectory, request.DirectoryExclusionPredicate, 1);
        return await this.ScanDirectoryAsync(request);
    }

    private Task<IndividualDetectorScanResult> ScanDirectoryAsync(ScanRequest request)
    {
        this.CurrentScanRequest = request;

        var filteredObservable = this.Scanner.GetFilteredComponentStreamObservable(request.SourceDirectory, this.SearchPatterns, request.ComponentRecorder);

        this.Logger?.LogVerbose($"Registered {this.GetType().FullName}");
        return this.ProcessAsync(filteredObservable, request.DetectorArgs);
    }

    /// <summary>
    /// Gets the file streams for the Detector's declared <see cref="SearchPatterns"/> as an <see cref="IEnumerable{IComponentStream}"/>.
    /// </summary>
    /// <param name="sourceDirectory">The directory to search.</param>
    /// <param name="exclusionPredicate">The exclusion predicate function.</param>
    /// <returns>Awaitable task with enumerable streams <see cref="IEnumerable{IComponentStream}"/> for the declared detector. </returns>
    protected Task<IEnumerable<IComponentStream>> GetFileStreamsAsync(DirectoryInfo sourceDirectory, ExcludeDirectoryPredicate exclusionPredicate) => Task.FromResult(this.ComponentStreamEnumerableFactory.GetComponentStreams(sourceDirectory, this.SearchPatterns, exclusionPredicate));

    private async Task<IndividualDetectorScanResult> ProcessAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
    {
        var processor = new ActionBlock<ProcessRequest>(async processRequest => await this.OnFileFound(processRequest, detectorArgs));

        var preprocessedObserbable = await this.OnPrepareDetection(processRequests, detectorArgs);

        await preprocessedObserbable.ForEachAsync(processRequest => processor.Post(processRequest));

        processor.Complete();

        await processor.Completion;

        await this.OnDetectionFinished();

        return new IndividualDetectorScanResult
        {
            ResultCode = ProcessingResultCode.Success,
            AdditionalTelemetryDetails = this.Telemetry,
        };
    }

    protected virtual Task<IObservable<ProcessRequest>> OnPrepareDetection(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs) => Task.FromResult(processRequests);

    protected abstract Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs);

    protected virtual Task OnDetectionFinished() => Task.CompletedTask;
}

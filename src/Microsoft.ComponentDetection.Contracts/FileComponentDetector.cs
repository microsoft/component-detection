namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Contracts.Utilities;
using Microsoft.Extensions.Logging;

/// <summary>Specialized base class for file based component detection.</summary>
public abstract class FileComponentDetector : IComponentDetector
{
    private const int DefaultCleanDepth = 5;

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
    protected virtual IList<string> SkippedFolders => new List<string> { };

    /// <summary>
    /// Gets or sets the active scan request -- only populated after a ScanDirectoryAsync is invoked. If ScanDirectoryAsync is overridden,
    ///  the overrider should ensure this property is populated.
    /// </summary>
    protected ScanRequest CurrentScanRequest { get; set; }

    public virtual bool NeedsAutomaticRootDependencyCalculation { get; protected set; }

    protected Dictionary<string, string> Telemetry { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// List of any any additional properties as key-value pairs that we would like to capture for the detector.
    /// </summary>
    public List<(string PropertyKey, string PropertyValue)> AdditionalProperties { get; set; } = new List<(string PropertyKey, string PropertyValue)>();

    protected IObservable<IComponentStream> ComponentStreams { get; private set; }

    protected virtual bool EnableParallelism { get; set; }

    /// <summary>
    /// Patterns of files and folders that should be cleaned up after the detector has run, if they were created by the detector.
    /// </summary>
    protected virtual IList<string> CleanupPatterns { get; set; }

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
            async processRequest => await this.OnFileFoundWithCleanupAsync(processRequest, detectorArgs, cleanupCreatedFiles, cancellationToken),
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
            AdditionalTelemetryDetails = this.Telemetry,
        };
    }

    protected virtual Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(processRequests);
    }

    protected abstract Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default);

    protected virtual Task OnDetectionFinishedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Takes a process and wraps it in a cleanup operation that will delete any files or
    /// directories created during the process that match the clean up patterns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task WithCleanupAsync(
        Func<ProcessRequest, IDictionary<string, string>, CancellationToken, Task> process,
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        bool cleanupCreatedFiles,
        CancellationToken cancellationToken = default)
    {
        if (process == null)
        {
            throw new ArgumentNullException(nameof(process));
        }

        if (!this.TryGetCleanupFileDirectory(processRequest, out var fileParentDirectory))
        {
            await process(processRequest, detectorArgs, cancellationToken).ConfigureAwait(false);
            return;
        }

        var (preExistingFiles, preExistingDirs) = DirectoryUtilities.GetFilesAndDirectories(fileParentDirectory, this.CleanupPatterns, DefaultCleanDepth);
        try
        {
            await process(processRequest, detectorArgs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Clean up any new files or directories created during the scan that match the clean up patterns
            var dryRun = !cleanupCreatedFiles;
            var dryRunStr = dryRun ? "[DRYRUN] " : string.Empty;
            var (newFiles, newDirs) = DirectoryUtilities.GetFilesAndDirectories(fileParentDirectory, this.CleanupPatterns, DefaultCleanDepth);
            var createdFiles = newFiles.Except(preExistingFiles).ToList();
            var createdDirs = newDirs.Except(preExistingDirs).ToList();

            foreach (var createdDir in createdDirs)
            {
                if (createdDir is null || !Directory.Exists(createdDir))
                {
                    continue;
                }

                try
                {
                    this.Logger.LogDebug("{DryRun}Cleaning up directory {Dir}", dryRunStr, createdDir);
                    if (!dryRun)
                    {
                        Directory.Delete(createdDir, true);
                    }
                }
                catch (Exception e)
                {
                    this.Logger.LogDebug(e, "{DryRun}Failed to delete directory {Dir}", dryRunStr, createdDir);
                }
            }

            foreach (var createdFile in createdFiles)
            {
                if (createdFile is null || !File.Exists(createdFile))
                {
                    continue;
                }

                try
                {
                    this.Logger.LogDebug("{DryRun}Cleaning up file {File}", dryRunStr, createdFile);
                    if (!dryRun)
                    {
                        File.Delete(createdFile);
                    }
                }
                catch (Exception e)
                {
                    this.Logger.LogDebug(e, "{DryRun}Failed to delete file {File}", dryRunStr, createdFile);
                }
            }
        }
    }

    private async Task OnFileFoundWithCleanupAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, bool cleanupCreatedFiles, CancellationToken cancellationToken = default) =>
        await this.WithCleanupAsync(this.OnFileFoundAsync, processRequest, detectorArgs, cleanupCreatedFiles, cancellationToken).ConfigureAwait(false);

    // Confirm that there are existing clean up patterns and that the process request has an existing directory.
    private bool TryGetCleanupFileDirectory(ProcessRequest processRequest, out string directory)
    {
        directory = string.Empty;
        if (this.CleanupPatterns != null
            && this.CleanupPatterns.Any()
            && processRequest?.ComponentStream?.Location != null
            && Path.GetDirectoryName(processRequest.ComponentStream.Location) != null
            && Directory.Exists(Path.GetDirectoryName(processRequest.ComponentStream.Location)))
        {
            directory = Path.GetDirectoryName(processRequest.ComponentStream.Location);
            return true;
        }

        return false;
    }
}

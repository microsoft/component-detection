#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a request to scan a directory for components.
/// </summary>
public class ScanRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScanRequest"/> class.
    /// </summary>
    /// <param name="sourceDirectory">The source directory to consider the working directory for the detection operation.</param>
    /// <param name="directoryExclusionPredicate">A predicate which evaluates directories, if the predicate returns true the directory will be excluded.</param>
    /// <param name="logger">The logger for this detection session.</param>
    /// <param name="detectorArgs">A dictionary of custom detector arguments supplied externally.</param>
    /// <param name="imagesToScan">Container images to scan.</param>
    /// <param name="componentRecorder">Detector component recorder.</param>
    /// <param name="maxThreads">Max number of threads to use for detection.</param>
    /// <param name="cleanupCreatedFiles">Whether or not to cleanup files that are created during detection.</param>
    /// <param name="sourceFileRoot">Directory where source files can be found. In most scenarios this will be the same as <paramref name="sourceDirectory"/> but source code can be a different folder.</param>
    public ScanRequest(DirectoryInfo sourceDirectory, ExcludeDirectoryPredicate directoryExclusionPredicate, ILogger logger, IDictionary<string, string> detectorArgs, IEnumerable<string> imagesToScan, IComponentRecorder componentRecorder, int maxThreads = 5, bool cleanupCreatedFiles = true, DirectoryInfo sourceFileRoot = null)
    {
        this.SourceDirectory = sourceDirectory;
        this.DirectoryExclusionPredicate = directoryExclusionPredicate;
        this.DetectorArgs = detectorArgs;
        this.ImagesToScan = imagesToScan;
        this.ComponentRecorder = componentRecorder;
        this.MaxThreads = maxThreads;
        this.CleanupCreatedFiles = cleanupCreatedFiles;
        this.SourceFileRoot = sourceFileRoot;
    }

    /// <summary>
    /// Gets the source directory to consider the working directory for the detection operation.
    /// </summary>
    public DirectoryInfo SourceDirectory { get; private set; }

    /// <summary>
    /// Directory where source files can be found.
    /// </summary>
    public DirectoryInfo SourceFileRoot { get; private set; }

    /// <summary>
    /// Gets a predicate which evaluates directories, if the predicate returns true the directory will be excluded.
    /// </summary>
    public ExcludeDirectoryPredicate DirectoryExclusionPredicate { get; private set; }

    /// <summary>
    /// Gets the dictionary of custom detector arguments supplied externally.
    /// </summary>
    public IDictionary<string, string> DetectorArgs { get; private set; }

    /// <summary>
    /// Gets the container images to scan.
    /// </summary>
    public IEnumerable<string> ImagesToScan { get; private set; }

    /// <summary>
    /// Gets the detector component recorder.
    /// </summary>
    public IComponentRecorder ComponentRecorder { get; private set; }

    /// <summary>
    /// Gets the maximum number of threads to use in parallel for executing the detection, assuming parallelism is
    /// enabled for the detector.
    /// </summary>
    public int MaxThreads { get; private set; }

    /// <summary>
    /// Whether or not to cleanup files that are created during detection, based on the rules provided in each detector.
    /// </summary>
    public bool CleanupCreatedFiles { get; private set; }
}

namespace Microsoft.ComponentDetection.Contracts
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>Request object for a component scan.</summary>
    public class ScanRequest
    {
        /// <summary>Creates a new ScanRequest.</summary>
        /// <param name="sourceDirectory">The source directory to consider the working directory for the detection operation.</param>
        /// <param name="directoryExclusionPredicate">A predicate which evaluates directories, if the predicate returns true the directory will be excluded.</param>
        /// <param name="logger">The logger for this detection session.</param>
        /// <param name="detectorArgs">A dictionary of custom detector arguments supplied externally.</param>
        /// <param name="imagesToScan">Container images to scan.</param>
        /// <param name="componentRecorder">Detector component recorder.</param>
        public ScanRequest(DirectoryInfo sourceDirectory, ExcludeDirectoryPredicate directoryExclusionPredicate, ILogger logger, IDictionary<string, string> detectorArgs, IEnumerable<string> imagesToScan, IComponentRecorder componentRecorder)
        {
            this.SourceDirectory = sourceDirectory;
            this.DirectoryExclusionPredicate = directoryExclusionPredicate;
            this.DetectorArgs = detectorArgs;
            this.ImagesToScan = imagesToScan;
            this.ComponentRecorder = componentRecorder;
        }

        /// <summary> Gets the source directory to consider the working directory for the detection operation.</summary>
        public DirectoryInfo SourceDirectory { get; private set; }

        /// <summary> Gets a predicate which evaluates directories, if the predicate returns true the directory will be excluded.</summary>
        public ExcludeDirectoryPredicate DirectoryExclusionPredicate { get; private set; }

        /// <summary> Gets the dictionary of custom detector arguments supplied externally.</summary>
        public IDictionary<string, string> DetectorArgs { get; private set; }

        public IEnumerable<string> ImagesToScan { get; private set; }

        public IComponentRecorder ComponentRecorder { get; private set; }
    }
}

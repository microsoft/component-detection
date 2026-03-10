#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.Extensions.Logging;

/// <summary>Specialized base class for file based component detection that enables cleanup after it runs.</summary>
public abstract class FileComponentDetectorWithCleanup : FileComponentDetector
{
    private const int DefaultCleanDepth = 5;

    /// <summary>
    /// SemaphoreSlim to ensure that only one cleanup process is running at a time.
    /// </summary>
    private readonly SemaphoreSlim cleanupSemaphore = new(1, 1);

    protected FileComponentDetectorWithCleanup(IFileUtilityService fileUtilityService, IDirectoryUtilityService directoryUtilityService)
    {
        this.FileUtilityService = fileUtilityService;
        this.DirectoryUtilityService = directoryUtilityService;
    }

    protected IFileUtilityService FileUtilityService { get; private set; }

    protected IDirectoryUtilityService DirectoryUtilityService { get; private set; }

    /// <summary>
    /// Patterns of files and folders that should be cleaned up after the detector has run, if they were created by the detector.
    /// </summary>
    protected virtual IList<string> CleanupPatterns { get; set; }

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

        // If there are no cleanup patterns, or the relevant file does not have a valid directory, run the process without even
        // determining the files that exist as there will be no subsequent cleanup process.
        if (this.FileUtilityService == null
            || this.DirectoryUtilityService == null
            || !this.TryGetCleanupFileDirectory(processRequest, out var fileParentDirectory)
            || !cleanupCreatedFiles)
        {
            await process(processRequest, detectorArgs, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Get the files and directories that match the cleanup pattern and exist before the process runs.
        var (preSuccess, preExistingFiles, preExistingDirs) = this.TryGetFilesAndDirectories(fileParentDirectory, this.CleanupPatterns, DefaultCleanDepth);

        await process(processRequest, detectorArgs, cancellationToken).ConfigureAwait(false);
        if (!preSuccess)
        {
            // return early if we failed to get the pre-existing files and directories, since no need for cleanup
            return;
        }

        // Ensure that only one cleanup process is running at a time, helping to prevent conflicts
        await this.cleanupSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Clean up any new files or directories created during the scan that match the clean up patterns.
            var (postSuccess, latestFiles, latestDirs) = this.TryGetFilesAndDirectories(fileParentDirectory, this.CleanupPatterns, DefaultCleanDepth);
            if (!postSuccess)
            {
                // return early if we failed to get the latest files and directories, since we will not be able
                // to determine what to clean up
                return;
            }

            var createdFiles = latestFiles.Except(preExistingFiles).ToList();
            var createdDirs = latestDirs.Except(preExistingDirs).ToList();

            foreach (var createdDir in createdDirs)
            {
                if (createdDir is null || !this.DirectoryUtilityService.Exists(createdDir))
                {
                    continue;
                }

                try
                {
                    this.Logger.LogDebug("Cleaning up directory {Dir}", createdDir);
                    this.DirectoryUtilityService.Delete(createdDir, true);
                }
                catch (Exception e)
                {
                    this.Logger.LogDebug(e, "Failed to delete directory {Dir}", createdDir);
                }
            }

            foreach (var createdFile in createdFiles)
            {
                if (createdFile is null || !this.FileUtilityService.Exists(createdFile))
                {
                    continue;
                }

                try
                {
                    this.Logger.LogDebug("Cleaning up file {File}", createdFile);
                    this.FileUtilityService.Delete(createdFile);
                }
                catch (Exception e)
                {
                    this.Logger.LogDebug(e, "Failed to delete file {File}", createdFile);
                }
            }
        }
        finally
        {
            _ = this.cleanupSemaphore.Release();
        }
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, bool cleanupCreatedFiles, CancellationToken cancellationToken = default) =>
        await this.WithCleanupAsync(this.OnFileFoundAsync, processRequest, detectorArgs, cleanupCreatedFiles, cancellationToken).ConfigureAwait(false);

    // Confirm that there are existing clean up patterns and that the process request has an existing directory.
    private bool TryGetCleanupFileDirectory(ProcessRequest processRequest, out string directory)
    {
        directory = string.Empty;
        if (this.CleanupPatterns != null
            && this.CleanupPatterns.Any()
            && processRequest?.ComponentStream?.Location != null
            && Path.GetDirectoryName(processRequest.ComponentStream.Location) != null
            && this.DirectoryUtilityService.Exists(Path.GetDirectoryName(processRequest.ComponentStream.Location)))
        {
            directory = Path.GetDirectoryName(processRequest.ComponentStream.Location);
            return true;
        }

        return false;
    }

    private (bool Success, HashSet<string> Files, HashSet<string> Directories) TryGetFilesAndDirectories(string root, IList<string> patterns, int depth)
    {
        try
        {
            var (files, directories) = this.DirectoryUtilityService.GetFilesAndDirectories(root, patterns, depth);
            return (true, files, directories);
        }
        catch (UnauthorizedAccessException e)
        {
            // log and return false if we are unauthorized to get files and directories
            this.Logger.LogDebug(e, "Unauthorized to get files and directories for {Root}", root);
            return (false, new HashSet<string>(), new HashSet<string>());
        }
    }
}

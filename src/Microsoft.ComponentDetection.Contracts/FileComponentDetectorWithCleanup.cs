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
            || !this.TryGetCleanupFileDirectory(processRequest, out var fileParentDirectory))
        {
            await process(processRequest, detectorArgs, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Get the files and directories that match the cleanup pattern and exist before the process runs.
        var (preExistingFiles, preExistingDirs) = this.DirectoryUtilityService.GetFilesAndDirectories(fileParentDirectory, this.CleanupPatterns, DefaultCleanDepth);
        try
        {
            await process(processRequest, detectorArgs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Ensure that only one cleanup process is running at a time, helping to prevent conflicts
            await this.cleanupSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Clean up any new files or directories created during the scan that match the clean up patterns.
                // If the cleanupCreatedFiles flag is set to false, this will be a dry run and will just log the files that it would clean.
                var dryRun = !cleanupCreatedFiles;
                var dryRunStr = dryRun ? "[DRYRUN] " : string.Empty;
                var (latestFiles, latestDirs) = this.DirectoryUtilityService.GetFilesAndDirectories(fileParentDirectory, this.CleanupPatterns, DefaultCleanDepth);
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
                        this.Logger.LogDebug("{DryRun}Cleaning up directory {Dir}", dryRunStr, createdDir);
                        if (!dryRun)
                        {
                            this.DirectoryUtilityService.Delete(createdDir, true);
                        }
                    }
                    catch (Exception e)
                    {
                        this.Logger.LogDebug(e, "{DryRun}Failed to delete directory {Dir}", dryRunStr, createdDir);
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
                        this.Logger.LogDebug("{DryRun}Cleaning up file {File}", dryRunStr, createdFile);
                        if (!dryRun)
                        {
                            this.FileUtilityService.Delete(createdFile);
                        }
                    }
                    catch (Exception e)
                    {
                        this.Logger.LogDebug(e, "{DryRun}Failed to delete file {File}", dryRunStr, createdFile);
                    }
                }
            }
            finally
            {
                _ = this.cleanupSemaphore.Release();
            }
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
}

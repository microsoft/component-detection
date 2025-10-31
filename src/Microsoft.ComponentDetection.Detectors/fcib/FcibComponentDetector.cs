namespace Microsoft.ComponentDetection.Detectors.Fcib;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detects foreign checked-in binaries (compiled executable files that are checked into source control).
/// </summary>
public class FcibComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private static readonly HashSet<string> BinaryExtensions =
    [
        ".DLL",
        ".EXE",
        ".SO",
        ".DYLIB",
        ".A",
        ".LIB",
        ".O",
        ".OBJ",
    ];

    private readonly IFileUtilityService fileUtilityService;

    public FcibComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IFileUtilityService fileUtilityService,
        ILogger<FcibComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    public override string Id => "Fcib";

    public override IList<string> SearchPatterns { get; } =
    [
        "*.dll",
        "*.exe",
        "*.so",
        "*.dylib",
        "*.a",
        "*.lib",
        "*.o",
        "*.obj",
    ];

    public override IEnumerable<string> Categories => ["Binary"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Fcib];

    public override int Version { get; } = 1;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        try
        {
            var fileExtension = Path.GetExtension(file.Location).ToUpperInvariant();

            // Only process files with binary extensions
            if (!BinaryExtensions.Contains(fileExtension))
            {
                return;
            }

            // Check if the file is in a typical build output or package cache directory
            var normalizedPath = file.Location.Replace(Path.DirectorySeparatorChar, '/');
            if (this.IsInExcludedDirectory(normalizedPath))
            {
                this.Logger.LogDebug("Skipping binary in build output or cache directory: {FilePath}", file.Location);
                return;
            }

            // Compute hash of the binary file
            string hash = null;
            try
            {
                using var stream = this.fileUtilityService.MakeFileStream(file.Location);
                using var sha256 = SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
                hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToUpperInvariant();
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Failed to compute hash for binary file: {FilePath}", file.Location);
            }

            var relativePath = this.GetRelativePath(file.Location);
            var component = new FcibComponent(relativePath, hash);
            var detectedComponent = new DetectedComponent(component);

            singleFileComponentRecorder.RegisterUsage(detectedComponent, isExplicitReferencedDependency: true);

            this.Logger.LogDebug("Detected foreign checked-in binary: {FilePath}", file.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Error while detecting foreign checked-in binary: {FilePath}", file.Location);
        }
    }

    private bool IsInExcludedDirectory(string normalizedPath)
    {
        var excludedDirectories = new[]
        {
            "/bin/",
            "/obj/",
            "/target/",
            "/build/",
            "/dist/",
            "/out/",
            "/.nuget/",
            "/node_modules/",
            "/packages/",
            "/.cargo/",
            "/.gradle/",
            "/.m2/",
            "/.pub-cache/",
            "/.cache/",
        };

        return excludedDirectories.Any(dir => normalizedPath.Contains(dir, StringComparison.OrdinalIgnoreCase));
    }

    private string GetRelativePath(string fullPath)
    {
        try
        {
            var sourceDirectory = this.CurrentScanRequest?.SourceDirectory?.FullName;
            if (!string.IsNullOrEmpty(sourceDirectory))
            {
                return Path.GetRelativePath(sourceDirectory, fullPath);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to compute relative path for: {FullPath}", fullPath);
        }

        return fullPath;
    }
}

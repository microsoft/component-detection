#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Enum representing which detection method was used.
/// </summary>
internal enum MavenDetectionMethod
{
    /// <summary>No detection performed.</summary>
    None,

    /// <summary>MvnCli was used successfully for all files.</summary>
    MvnCliOnly,

    /// <summary>Static parser was used for all files (MvnCli not available or failed completely).</summary>
    StaticParserOnly,

    /// <summary>MvnCli succeeded for some files, static parser used for failed files.</summary>
    Mixed,
}

/// <summary>
/// Enum representing why fallback occurred.
/// </summary>
internal enum MavenFallbackReason
{
    /// <summary>No fallback was needed.</summary>
    None,

    /// <summary>Maven CLI was explicitly disabled via the CD_MAVEN_DISABLE_CLI environment variable.</summary>
    MvnCliDisabledByUser,

    /// <summary>Maven CLI was not available in PATH.</summary>
    MavenCliNotAvailable,

    /// <summary>MvnCli failed due to authentication error (401/403).</summary>
    AuthenticationFailure,

    /// <summary>MvnCli failed due to other reasons.</summary>
    OtherMvnCliFailure,
}

/// <summary>
/// Experimental Maven detector that combines MvnCli detection with static pom.xml parsing fallback.
/// Runs MvnCli detection first (like standard MvnCliComponentDetector), then checks if detection
/// produced any results. If MvnCli fails for any pom.xml, falls back to static parsing for failed files.
/// </summary>
public class MavenWithFallbackDetector : FileComponentDetector, IExperimentalDetector
{
    /// <summary>
    /// Environment variable to disable MvnCli and use only static pom.xml parsing.
    /// Set to "true" to disable MvnCli detection.
    /// Usage: Set CD_MAVEN_DISABLE_CLI=true as a pipeline/environment variable.
    /// </summary>
    internal const string DisableMvnCliEnvVar = "CD_MAVEN_DISABLE_CLI";

    private const string MavenManifest = "pom.xml";
    private const string MavenXmlNamespace = "http://maven.apache.org/POM/4.0.0";
    private const string ProjNamespace = "proj";
    private const string DependencyNode = "//proj:dependency";

    private const string GroupIdSelector = "groupId";
    private const string ArtifactIdSelector = "artifactId";
    private const string VersionSelector = "version";

    private static readonly Regex VersionRegex = new(
        @"^\$\{(.*)\}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Auth error patterns to detect in Maven error output
    private static readonly string[] AuthErrorPatterns =
    [
        "401",
        "403",
        "Unauthorized",
        "Access denied",
    ];

    // Pattern to extract failed endpoint URL from Maven error messages
    private static readonly Regex EndpointRegex = new(
        @"https?://[^\s\]\)>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Maximum time allowed for the OnPrepareDetectionAsync phase.
    /// This is a safety guardrail to prevent hangs in the experimental detector.
    /// Most repos should complete the full Maven CLI scan within this window.
    /// </summary>
    private static readonly TimeSpan PrepareDetectionTimeout = TimeSpan.FromMinutes(5);

    private readonly IMavenCommandService mavenCommandService;
    private readonly IEnvironmentVariableService envVarService;
    private readonly IFileUtilityService fileUtilityService;

    // Two-pass static parsing: collect variables first, then resolve components
    private readonly ConcurrentDictionary<string, string> collectedVariables = new();
    private readonly ConcurrentQueue<PendingComponent> pendingComponents = new();

    // Track Maven parent-child relationships for proper variable resolution
    private readonly ConcurrentDictionary<string, string> mavenParentChildRelationships = new();

    // Track processed Maven projects by coordinates (groupId:artifactId -> file path)
    private readonly ConcurrentDictionary<string, string> processedMavenProjects = new();

    // Track files that couldn't establish parent relationships during first pass (for second pass re-evaluation)
    private readonly ConcurrentQueue<(string FilePath, string ParentGroupId, string ParentArtifactId)> unresolvedParentRelationships = new();

    // Track original pom.xml files for potential fallback
    private readonly ConcurrentQueue<ProcessRequest> originalPomFiles = [];

    // Track Maven CLI errors for analysis
    private readonly ConcurrentQueue<string> mavenCliErrors = [];
    private readonly ConcurrentQueue<string> failedEndpoints = [];

    /// <summary>
    /// Cache for parent POM lookups to avoid repeated file system operations.
    /// Key: current file path, Value: parent POM path or empty string if not found.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> parentPomCache = new();

    // Telemetry tracking
    private MavenDetectionMethod usedDetectionMethod = MavenDetectionMethod.None;
    private MavenFallbackReason fallbackReason = MavenFallbackReason.None;
    private int mvnCliComponentCount;
    private int staticParserComponentCount;
    private bool mavenCliAvailable;

    public MavenWithFallbackDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IMavenCommandService mavenCommandService,
        IEnvironmentVariableService envVarService,
        IFileUtilityService fileUtilityService,
        ILogger<MavenWithFallbackDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.mavenCommandService = mavenCommandService;
        this.envVarService = envVarService;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    public override string Id => MavenConstants.MavenWithFallbackDetectorId;

    public override IList<string> SearchPatterns => [MavenManifest];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Maven];

    public override int Version => 2;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Maven)];

    // Normalizes a directory path by ensuring it ends with a directory separator.
    // This prevents false matches like "C:\foo" matching "C:\foobar".
    private static string NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var lastChar = path[^1];
        return lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool IsAuthenticationError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        // Use ReadOnlySpan for more efficient string searching
        var messageSpan = errorMessage.AsSpan();
        foreach (var pattern in AuthErrorPatterns)
        {
            if (messageSpan.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void LogDebugWithId(string message) =>
        this.Logger.LogDebug("{DetectorId}: {Message}", this.Id, message);

    private void LogInfo(string message) =>
        this.Logger.LogInformation("{DetectorId}: {Message}", this.Id, message);

    private void LogWarning(string message) =>
        this.Logger.LogWarning("{DetectorId}: {Message}", this.Id, message);

    /// <summary>
    /// Resets all per-scan state to prevent stale data from leaking between scans.
    /// This is critical because detectors are registered as singletons.
    /// </summary>
    private void ResetScanState()
    {
        // Clear all concurrent collections
        this.collectedVariables.Clear();
        this.mavenParentChildRelationships.Clear();
        this.processedMavenProjects.Clear();
        this.parentPomCache.Clear();

        // Drain all concurrent queues
        while (this.pendingComponents.TryDequeue(out _))
        {
            // Intentionally empty - just draining the queue
        }

        while (this.unresolvedParentRelationships.TryDequeue(out _))
        {
            // Intentionally empty - just draining the queue
        }

        while (this.originalPomFiles.TryDequeue(out _))
        {
            // Intentionally empty - just draining the queue
        }

        while (this.mavenCliErrors.TryDequeue(out _))
        {
            // Intentionally empty - just draining the queue
        }

        while (this.failedEndpoints.TryDequeue(out _))
        {
            // Intentionally empty - just draining the queue
        }

        // Reset telemetry counters and flags
        this.usedDetectionMethod = MavenDetectionMethod.None;
        this.fallbackReason = MavenFallbackReason.None;
        this.mvnCliComponentCount = 0;
        this.staticParserComponentCount = 0;
        this.mavenCliAvailable = false;
    }

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        // Reset all per-scan state to prevent stale data from previous scans
        // This is critical because detectors are registered as singletons
        this.ResetScanState();

        // Wrap the entire method in a try-catch with timeout to protect against hangs.
        // OnPrepareDetectionAsync doesn't have the same guardrails as OnFileFoundAsync,
        // so we need to be extra careful in this experimental detector.
        try
        {
            using var timeoutCts = new CancellationTokenSource(PrepareDetectionTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            return await this.OnPrepareDetectionCoreAsync(processRequests, linkedCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not user cancellation)
            this.LogWarning($"OnPrepareDetectionAsync timed out after {PrepareDetectionTimeout.TotalMinutes} minutes. Falling back to static pom.xml parsing.");
            this.Telemetry["TimedOut"] = "true";
            this.fallbackReason = MavenFallbackReason.OtherMvnCliFailure;
            this.usedDetectionMethod = MavenDetectionMethod.Mixed;
            return processRequests;
        }
        catch (Exception ex)
        {
            // Unexpected error - log and fall back to static parsing
            this.LogWarning($"OnPrepareDetectionAsync failed with unexpected error: {ex.Message}. Falling back to static pom.xml parsing.");
            this.Telemetry["PrepareDetectionError"] = ex.GetType().Name;
            this.fallbackReason = MavenFallbackReason.OtherMvnCliFailure;
            this.usedDetectionMethod = MavenDetectionMethod.Mixed;
            return processRequests;
        }
    }

    /// <summary>
    /// Core implementation of OnPrepareDetectionAsync, called within the timeout wrapper.
    /// </summary>
    private async Task<IObservable<ProcessRequest>> OnPrepareDetectionCoreAsync(
        IObservable<ProcessRequest> processRequests,
        CancellationToken cancellationToken)
    {
        // Check if we should skip Maven CLI and use static parsing only
        if (this.ShouldSkipMavenCli())
        {
            return processRequests;
        }

        // Check if Maven CLI is available
        if (!await this.TryInitializeMavenCliAsync())
        {
            return processRequests;
        }

        // Create per-scan dictionary to track nested pom.xml mappings
        // This prevents state accumulation across scans since detectors are singletons
        var parentPomDictionary = new ConcurrentDictionary<string, IList<ProcessRequest>>(StringComparer.OrdinalIgnoreCase);

        // Run Maven CLI detection on all pom.xml files
        // Returns deps files for CLI successes, pom.xml files for CLI failures
        return await this.RunMavenCliDetectionAsync(processRequests, parentPomDictionary, cancellationToken);
    }

    /// <summary>
    /// Checks if Maven CLI should be skipped due to environment variable configuration.
    /// </summary>
    /// <returns>True if Maven CLI should be skipped; otherwise, false.</returns>
    private bool ShouldSkipMavenCli()
    {
        if (this.envVarService.IsEnvironmentVariableValueTrue(DisableMvnCliEnvVar))
        {
            this.LogInfo($"MvnCli detection disabled via {DisableMvnCliEnvVar} environment variable. Using static pom.xml parsing only.");
            this.usedDetectionMethod = MavenDetectionMethod.StaticParserOnly;
            this.fallbackReason = MavenFallbackReason.MvnCliDisabledByUser;
            this.mavenCliAvailable = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if Maven CLI is available.
    /// </summary>
    /// <returns>True if Maven CLI is available; otherwise, false.</returns>
    private async Task<bool> TryInitializeMavenCliAsync()
    {
        this.mavenCliAvailable = await this.mavenCommandService.MavenCLIExistsAsync();

        if (!this.mavenCliAvailable)
        {
            this.LogInfo("Maven CLI not found in PATH. Will use static pom.xml parsing only.");
            this.usedDetectionMethod = MavenDetectionMethod.StaticParserOnly;
            this.fallbackReason = MavenFallbackReason.MavenCliNotAvailable;
            return false;
        }

        this.LogDebugWithId("Maven CLI is available. Running MvnCli detection.");
        return true;
    }

    /// <summary>
    /// Runs Maven CLI detection on all root pom.xml files.
    /// For each pom.xml, if CLI succeeds, the deps file is added to results.
    /// If CLI fails, all pom.xml files under that directory are added for static parsing fallback.
    /// </summary>
    /// <param name="processRequests">The incoming process requests.</param>
    /// <param name="parentPomDictionary">Dictionary to track nested pom.xml mappings for fallback scenarios.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An observable of process requests (deps files for CLI success, pom.xml for CLI failure).</returns>
    private async Task<IObservable<ProcessRequest>> RunMavenCliDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        ConcurrentDictionary<string, IList<ProcessRequest>> parentPomDictionary,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentQueue<ProcessRequest>();
        var failedDirectories = new ConcurrentQueue<string>();
        var cliSuccessCount = 0;
        var cliFailureCount = 0;

        // Process pom.xml files sequentially to match MvnCliComponentDetector behavior.
        // Sequential execution avoids Maven local repository lock contention and
        // reduces memory pressure from concurrent Maven JVM processes.
        var processPomFile = new ActionBlock<ProcessRequest>(
            async processRequest =>
        {
            // Check for cancellation before processing each pom.xml
            cancellationToken.ThrowIfCancellationRequested();

            // Store original pom.xml for telemetry
            this.originalPomFiles.Enqueue(processRequest);

            var pomFile = processRequest.ComponentStream;
            var pomDir = Path.GetDirectoryName(pomFile.Location);
            var depsFileName = this.mavenCommandService.BcdeMvnDependencyFileName;
            var depsFilePath = Path.Combine(pomDir, depsFileName);

            // Generate dependency file using Maven CLI.
            // Note: If both MvnCliComponentDetector and this detector are enabled,
            // they may run Maven CLI on the same pom.xml independently.
            var result = await this.mavenCommandService.GenerateDependenciesFileAsync(
                processRequest,
                cancellationToken);

            if (result.Success)
            {
                // CLI succeeded - verify deps file was generated
                // Use existence check to avoid redundant I/O (file will be read during directory scan)
                if (this.fileUtilityService.Exists(depsFilePath))
                {
                    // File reader registration is now handled in GenerateDependenciesFileAsync
                    Interlocked.Increment(ref cliSuccessCount);
                }
                else
                {
                    // CLI reported success but deps file is missing - treat as failure
                    Interlocked.Increment(ref cliFailureCount);
                    failedDirectories.Enqueue(pomDir);
                    this.LogWarning($"Maven CLI succeeded but deps file not found: {depsFilePath}");
                }
            }
            else
            {
                // CLI failed - track directory for nested pom.xml scanning
                Interlocked.Increment(ref cliFailureCount);
                failedDirectories.Enqueue(pomDir);

                // Capture error output for later analysis
                if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                {
                    this.mavenCliErrors.Enqueue(result.ErrorOutput);
                }
            }
        },
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
            });

        await this.RemoveNestedPomXmls(processRequests, parentPomDictionary, cancellationToken).ForEachAsync(
            processRequest =>
            {
                processPomFile.Post(processRequest);
            },
            cancellationToken);

        processPomFile.Complete();
        await processPomFile.Completion;

        // For failed directories, scan and add all pom.xml files for static parsing
        if (!failedDirectories.IsEmpty)
        {
            foreach (var failedDir in failedDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalizedFailedDir = NormalizeDirectoryPath(failedDir);
                if (parentPomDictionary.TryGetValue(normalizedFailedDir, out var staticParsingRequests))
                {
                    // Note: staticParsingRequests is already in parent-first order due to the sorted processing
                    // during dictionary building in RemoveNestedPomXmls
                    foreach (var request in staticParsingRequests)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        results.Enqueue(request);
                    }
                }
            }
        }

        // Determine detection method based on results
        this.DetermineDetectionMethod(cliSuccessCount, cliFailureCount);

        this.LogDebugWithId($"Maven CLI processing complete: {cliSuccessCount} succeeded, {cliFailureCount} failed out of {this.originalPomFiles.Count} root pom.xml files. Retrieving generated dependency graphs.");

        // Use comprehensive directory scanning after Maven CLI execution to find all generated dependency files
        // This ensures we find dependency files from submodules even if Maven CLI was only run on parent pom.xml
        var allGeneratedDependencyFiles = this.ComponentStreamEnumerableFactory
            .GetComponentStreams(
                this.CurrentScanRequest.SourceDirectory,
                [this.mavenCommandService.BcdeMvnDependencyFileName],
                this.CurrentScanRequest.DirectoryExclusionPredicate)
            .Select(componentStream =>
            {
                // Read and store content to avoid stream disposal issues
                // Note: Cleanup coordination is handled in OnFileFoundAsync to avoid duplicate work
                using var reader = new StreamReader(componentStream.Stream);
                var content = reader.ReadToEnd();
                return new ProcessRequest
                {
                    ComponentStream = new ComponentStream
                    {
                        Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                        Location = componentStream.Location,
                        Pattern = componentStream.Pattern,
                    },
                    SingleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(
                        Path.Combine(Path.GetDirectoryName(componentStream.Location), MavenManifest)),
                };
            });

        // Combine dependency files from CLI success with pom.xml files from CLI failures
        return results.Concat(allGeneratedDependencyFiles).ToObservable();
    }

    /// <summary>
    /// Determines the detection method based on CLI success/failure counts and analyzes any failures.
    /// </summary>
    /// <param name="cliSuccessCount">Number of successful CLI executions.</param>
    /// <param name="cliFailureCount">Number of failed CLI executions.</param>
    private void DetermineDetectionMethod(int cliSuccessCount, int cliFailureCount)
    {
        if (cliFailureCount == 0 && cliSuccessCount > 0)
        {
            this.usedDetectionMethod = MavenDetectionMethod.MvnCliOnly;
            this.LogDebugWithId("All pom.xml files processed successfully with Maven CLI.");
        }
        else if (cliFailureCount > 0)
        {
            this.usedDetectionMethod = MavenDetectionMethod.Mixed;
            this.LogWarning($"Maven CLI failed for {cliFailureCount} pom.xml files. Using mixed detection.");
            this.AnalyzeMvnCliFailure();
        }
    }

    protected override Task OnFileFoundAsync(
        ProcessRequest processRequest,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
        var pattern = processRequest.ComponentStream.Pattern;

        if (pattern == this.mavenCommandService.BcdeMvnDependencyFileName)
        {
            // Process MvnCli result
            this.ProcessMvnCliResult(processRequest);
        }
        else
        {
            // Process via static XML parsing
            this.ProcessPomFileStatically(processRequest);
        }

        return Task.CompletedTask;
    }

    protected override Task OnDetectionFinishedAsync()
    {
        // Second pass: resolve any parent relationships that couldn't be resolved during first pass
        // This handles cases where parent POM was processed after child POM
        this.ResolveUnresolvedParentRelationships();

        // Third pass: resolve all pending components with collected variables and complete hierarchy
        this.ResolvePendingComponents();

        // Record telemetry - cache string conversions
        var detectionMethodStr = this.usedDetectionMethod.ToString();
        var fallbackReasonStr = this.fallbackReason.ToString();
        var mvnCliCountStr = this.mvnCliComponentCount.ToString();
        var staticCountStr = this.staticParserComponentCount.ToString();

        this.Telemetry["DetectionMethod"] = detectionMethodStr;
        this.Telemetry["FallbackReason"] = fallbackReasonStr;
        this.Telemetry["MvnCliComponentCount"] = mvnCliCountStr;
        this.Telemetry["StaticParserComponentCount"] = staticCountStr;
        this.Telemetry["TotalComponentCount"] = (this.mvnCliComponentCount + this.staticParserComponentCount).ToString();
        this.Telemetry["MavenCliAvailable"] = this.mavenCliAvailable.ToString();
        this.Telemetry["OriginalPomFileCount"] = this.originalPomFiles.Count.ToString();
        this.Telemetry["CollectedVariableCount"] = this.collectedVariables.Count.ToString();
        this.Telemetry["PendingComponentCount"] = this.pendingComponents.Count.ToString();

        if (!this.failedEndpoints.IsEmpty)
        {
            this.Telemetry["FailedEndpoints"] = string.Join(";", this.failedEndpoints.Distinct().Take(10));
        }

        this.LogInfo($"Detection completed. Method: {detectionMethodStr}, " +
                     $"FallbackReason: {fallbackReasonStr}, " +
                     $"MvnCli components: {mvnCliCountStr}, " +
                     $"Static parser components: {staticCountStr}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes Maven CLI failure by checking logged errors for authentication issues.
    /// </summary>
    private void AnalyzeMvnCliFailure()
    {
        // Check if any recorded errors indicate authentication failure
        var hasAuthError = this.mavenCliErrors.Any(IsAuthenticationError);

        if (hasAuthError)
        {
            this.fallbackReason = MavenFallbackReason.AuthenticationFailure;

            // Extract failed endpoints from error messages
            foreach (var endpoint in this.mavenCliErrors.SelectMany(this.ExtractFailedEndpoints))
            {
                this.failedEndpoints.Enqueue(endpoint);
            }

            this.LogAuthErrorGuidance();
        }
        else
        {
            this.fallbackReason = MavenFallbackReason.OtherMvnCliFailure;
            this.LogWarning("Maven CLI failed. Check Maven logs for details.");
        }
    }

    private void ProcessMvnCliResult(ProcessRequest processRequest)
    {
        this.mavenCommandService.ParseDependenciesFile(processRequest);

        // Count components registered to this specific file's recorder to avoid race conditions
        // when OnFileFoundAsync runs concurrently for multiple files.
        var componentsInFile = processRequest.SingleFileComponentRecorder.GetDetectedComponents().Count;
        Interlocked.Add(ref this.mvnCliComponentCount, componentsInFile);
    }

    private void ProcessPomFileStatically(ProcessRequest processRequest)
    {
        var file = processRequest.ComponentStream;
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var filePath = file.Location;

        try
        {
            var document = new XmlDocument();
            document.Load(file.Stream);

            // Single XML parsing pass: create namespace manager once
            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace(ProjNamespace, MavenXmlNamespace);

            // Collect variables from this document into a local dictionary first
            var localVariables = new Dictionary<string, string>();
            this.CollectVariablesFromDocument(document, namespaceManager, filePath, localVariables);

            // Batch add local variables to global collection for better performance
            // Key format: "filePath::variableName" enables Maven hierarchy-aware lookup
            if (localVariables.Count > 0)
            {
                var keyBuilder = new StringBuilder(filePath.Length + 64); // Pre-allocate capacity
                var filePathWithSeparator = filePath + "::";

                foreach (var (variableName, variableValue) in localVariables)
                {
                    keyBuilder.Clear();
                    keyBuilder.Append(filePathWithSeparator).Append(variableName);
                    var key = keyBuilder.ToString();

                    this.collectedVariables.AddOrUpdate(key, variableValue, (_, _) => variableValue);
                }

                this.Logger.LogDebug("MavenWithFallback: Collected {Count} variables from {File}", localVariables.Count, Path.GetFileName(filePath));
            }

            // First pass: collect dependencies (may have unresolved variables)
            var dependencyList = document.SelectNodes(DependencyNode, namespaceManager);

            foreach (XmlNode dependency in dependencyList)
            {
                var groupId = dependency[GroupIdSelector]?.InnerText;
                var artifactId = dependency[ArtifactIdSelector]?.InnerText;

                if (groupId == null || artifactId == null)
                {
                    continue;
                }

                var version = dependency[VersionSelector];
                if (version != null && !version.InnerText.Contains(','))
                {
                    var versionRef = version.InnerText.Trim('[', ']');

                    if (versionRef.StartsWith("${"))
                    {
                        // Only resolve immediately if local variable exists (highest priority)
                        // Otherwise, defer to second pass to ensure proper hierarchy-aware resolution
                        var resolvedVersion = this.ResolveVersionFromLocalOnly(versionRef, localVariables);
                        if (!resolvedVersion.StartsWith("${"))
                        {
                            // Local variable found - resolve immediately (highest priority)
                            var component = new MavenComponent(groupId, artifactId, resolvedVersion);
                            var detectedComponent = new DetectedComponent(component);
                            singleFileComponentRecorder.RegisterUsage(detectedComponent);
                            Interlocked.Increment(ref this.staticParserComponentCount);
                        }
                        else
                        {
                            // No local variable - defer to second pass for hierarchy-aware resolution
                            // This ensures we consider all variable definitions before resolving
                            this.pendingComponents.Enqueue(new PendingComponent(
                                groupId,
                                artifactId,
                                versionRef,
                                singleFileComponentRecorder,
                                filePath));
                        }
                    }
                    else
                    {
                        // Direct version - register immediately
                        var component = new MavenComponent(groupId, artifactId, versionRef);
                        var detectedComponent = new DetectedComponent(component);
                        singleFileComponentRecorder.RegisterUsage(detectedComponent);
                        Interlocked.Increment(ref this.staticParserComponentCount);
                    }
                }
                else
                {
                    this.Logger.LogDebug(
                        "Version string for component {Group}/{Artifact} is invalid or unsupported and a component will not be recorded.",
                        groupId,
                        artifactId);
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read file {Path}", filePath);
        }
    }

    /// <summary>
    /// Collects all variable definitions from a POM document into the provided local dictionary.
    /// Optimized to reuse XmlNamespaceManager and minimize XPath queries.
    /// </summary>
    /// <param name="document">The XML document to scan for variables.</param>
    /// <param name="namespaceManager">Pre-configured namespace manager to reuse.</param>
    /// <param name="filePath">The file path for logging purposes.</param>
    /// <param name="localVariables">Local dictionary to collect variables into.</param>
    private void CollectVariablesFromDocument(XmlDocument document, XmlNamespaceManager namespaceManager, string filePath, Dictionary<string, string> localVariables)
    {
        try
        {
            // Query project coordinates once - used for both variable collection and project tracking
            var projectGroupIdNode = document.SelectSingleNode("/proj:project/proj:groupId", namespaceManager);
            var projectArtifactIdNode = document.SelectSingleNode("/proj:project/proj:artifactId", namespaceManager);
            var projectVersionNode = document.SelectSingleNode("/proj:project/proj:version", namespaceManager);

            // Track this project by Maven coordinates for parent resolution (reuses queried nodes)
            this.TrackMavenProjectCoordinates(document, namespaceManager, filePath, projectGroupIdNode, projectArtifactIdNode);

            // Parse Maven parent relationship to build proper hierarchy
            this.ParseMavenParentRelationship(document, namespaceManager, filePath);

            // Collect properties variables from ALL properties sections (handles malformed XML with multiple <properties>)
            var propertiesNodes = document.SelectNodes("//proj:properties", namespaceManager);
            if (propertiesNodes?.Count > 0)
            {
                if (propertiesNodes.Count > 1)
                {
                    this.Logger.LogDebug("MavenWithFallback: Found {Count} properties sections in {File}", propertiesNodes.Count, Path.GetFileName(filePath));
                }

                foreach (XmlNode propertiesNode in propertiesNodes)
                {
                    foreach (XmlNode propertyNode in propertiesNode.ChildNodes)
                    {
                        if (propertyNode.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(propertyNode.InnerText))
                        {
                            // Later properties sections override earlier ones (last wins - Maven behavior)
                            localVariables[propertyNode.Name] = propertyNode.InnerText;
                        }
                    }
                }
            }

            // Collect project-level variables from already-queried nodes
            if (projectVersionNode != null && !string.IsNullOrWhiteSpace(projectVersionNode.InnerText))
            {
                localVariables["version"] = projectVersionNode.InnerText;
                localVariables["project.version"] = projectVersionNode.InnerText;
            }

            if (projectGroupIdNode != null && !string.IsNullOrWhiteSpace(projectGroupIdNode.InnerText))
            {
                localVariables["groupId"] = projectGroupIdNode.InnerText;
                localVariables["project.groupId"] = projectGroupIdNode.InnerText;
            }

            if (projectArtifactIdNode != null && !string.IsNullOrWhiteSpace(projectArtifactIdNode.InnerText))
            {
                localVariables["artifactId"] = projectArtifactIdNode.InnerText;
                localVariables["project.artifactId"] = projectArtifactIdNode.InnerText;
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to collect variables from file {Path}", filePath);
        }
    }

    /// <summary>
    /// Parses Maven parent relationship from pom.xml to build proper inheritance hierarchy.
    /// This is needed for Maven-compliant variable resolution that respects parent-child relationships.
    /// </summary>
    /// <param name="document">The XML document to parse.</param>
    /// <param name="namespaceManager">XML namespace manager for Maven POM.</param>
    /// <param name="currentFilePath">Current pom.xml file path.</param>
    private void ParseMavenParentRelationship(XmlDocument document, XmlNamespaceManager namespaceManager, string currentFilePath)
    {
        try
        {
            // Query parent element once and access children directly (more efficient than union XPath)
            var parentNode = document.SelectSingleNode("/proj:project/proj:parent", namespaceManager);

            if (parentNode != null)
            {
                var parentGroupId = parentNode["groupId"]?.InnerText;
                var parentArtifactId = parentNode["artifactId"]?.InnerText;

                if (!string.IsNullOrWhiteSpace(parentArtifactId))
                {
                    // Try to find parent pom.xml file by searching processed files for matching artifactId
                    // This works if parent was processed before child
                    var parentPath = this.FindParentPomByArtifactId(parentGroupId, parentArtifactId, currentFilePath);
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        this.mavenParentChildRelationships[currentFilePath] = parentPath;
                        this.Logger.LogDebug(
                            "MavenWithFallback: Parsed parent relationship: {Child} → {Parent}",
                            Path.GetFileName(currentFilePath),
                            Path.GetFileName(parentPath));
                    }
                    else
                    {
                        // Parent not found yet - queue for second pass resolution after all files are processed
                        this.unresolvedParentRelationships.Enqueue((currentFilePath, parentGroupId, parentArtifactId));
                        this.Logger.LogDebug(
                            "MavenWithFallback: Queued unresolved parent relationship for {Child} → {ParentArtifactId}",
                            Path.GetFileName(currentFilePath),
                            parentArtifactId);
                    }
                }
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to parse parent relationship from {FilePath}", currentFilePath);
        }
    }

    /// <summary>
    /// Finds parent pom.xml file path by Maven coordinates (groupId:artifactId).
    /// First searches by coordinates among processed projects, then falls back to directory traversal.
    /// </summary>
    /// <param name="parentGroupId">Parent groupId to match.</param>
    /// <param name="parentArtifactId">Parent artifactId to match.</param>
    /// <param name="currentFilePath">Current file path to start searching from.</param>
    /// <returns>Parent pom.xml file path, or empty string if not found.</returns>
    private string FindParentPomByArtifactId(string parentGroupId, string parentArtifactId, string currentFilePath)
    {
        // Use cache to avoid repeated operations for the same file
        return this.parentPomCache.GetOrAdd(currentFilePath, filePath =>
        {
            try
            {
                // First, try to find by Maven coordinates (handles sibling projects)
                if (!string.IsNullOrWhiteSpace(parentArtifactId))
                {
                    var coordinateKey = string.IsNullOrWhiteSpace(parentGroupId)
                        ? parentArtifactId
                        : $"{parentGroupId}:{parentArtifactId}";

                    if (this.processedMavenProjects.TryGetValue(coordinateKey, out var coordinateBasedPath))
                    {
                        this.Logger.LogDebug(
                            "MavenWithFallback: Found parent {ParentCoordinate} at {Path} for {Child}",
                            coordinateKey,
                            Path.GetFileName(coordinateBasedPath),
                            Path.GetFileName(filePath));
                        return coordinateBasedPath;
                    }
                }

                // Fallback: Maven convention parent directory search
                var currentDir = Path.GetDirectoryName(filePath);
                var parentDir = Path.GetDirectoryName(currentDir);

                // Track visited directories to prevent infinite loops from circular directory structures
                var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (!string.IsNullOrEmpty(parentDir))
                {
                    // Prevent infinite loops from circular directory references or file system anomalies
                    if (!visitedDirectories.Add(parentDir))
                    {
                        this.Logger.LogDebug(
                            "MavenWithFallback: Circular directory reference detected while searching for parent POM, breaking at {Directory}",
                            parentDir);
                        break;
                    }

                    var parentPomPath = Path.Combine(parentDir, "pom.xml");
                    if (this.fileUtilityService.Exists(parentPomPath) &&
                        !string.Equals(parentPomPath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return parentPomPath;
                    }

                    var nextParentDir = Path.GetDirectoryName(parentDir);
                    if (string.Equals(nextParentDir, parentDir, StringComparison.OrdinalIgnoreCase))
                    {
                        break; // Reached file system root
                    }

                    parentDir = nextParentDir;
                }

                return string.Empty; // Not found
            }
            catch (Exception ex)
            {
                this.Logger.LogDebug(ex, "Error finding parent POM for {FilePath}", Path.GetFileName(filePath));
                return string.Empty;
            }
        });
    }

    /// <summary>
    /// Tracks a Maven project by its coordinates to enable coordinate-based parent resolution.
    /// </summary>
    /// <param name="document">The XML document to parse.</param>
    /// <param name="namespaceManager">XML namespace manager for Maven POM.</param>
    /// <param name="filePath">Current pom.xml file path.</param>
    /// <param name="groupIdNode">Pre-queried groupId node (can be null).</param>
    /// <param name="artifactIdNode">Pre-queried artifactId node (can be null).</param>
    private void TrackMavenProjectCoordinates(XmlDocument document, XmlNamespaceManager namespaceManager, string filePath, XmlNode groupIdNode, XmlNode artifactIdNode)
    {
        try
        {
            // If project doesn't have its own groupId, try to get it from parent
            groupIdNode ??= document.SelectSingleNode("/proj:project/proj:parent/proj:groupId", namespaceManager);

            if (artifactIdNode != null && !string.IsNullOrWhiteSpace(artifactIdNode.InnerText))
            {
                var groupId = groupIdNode?.InnerText;
                var artifactId = artifactIdNode.InnerText;

                // Store with both artifactId-only and groupId:artifactId keys for flexible lookup
                this.processedMavenProjects.TryAdd(artifactId, filePath);
                if (!string.IsNullOrWhiteSpace(groupId))
                {
                    this.processedMavenProjects.TryAdd($"{groupId}:{artifactId}", filePath);
                }

                this.Logger.LogDebug(
                    "MavenWithFallback: Tracked project {GroupId}:{ArtifactId} at {Path}",
                    groupId ?? "(inherited)",
                    artifactId,
                    Path.GetFileName(filePath));
            }
        }
        catch (Exception e)
        {
            this.Logger.LogDebug(e, "Failed to track Maven project coordinates from {Path}", filePath);
        }
    }

    /// <summary>
    /// Resolves a version template using only local variables from the current file.
    /// This ensures immediate resolution only when the variable is defined in the same file (highest priority).
    /// </summary>
    /// <param name="versionTemplate">The version template with variables (e.g., "${revision}").</param>
    /// <param name="localVariables">Local variables from the current file.</param>
    /// <returns>The resolved version string, or the original template if local variable not found.</returns>
    private string ResolveVersionFromLocalOnly(string versionTemplate, Dictionary<string, string> localVariables)
    {
        var resolvedVersion = versionTemplate;
        var match = VersionRegex.Match(versionTemplate);

        if (match.Success)
        {
            var variable = match.Groups[1].Captures[0].ToString();

            // Only check local variables (same file priority)
            if (localVariables.TryGetValue(variable, out var localReplacement))
            {
                resolvedVersion = versionTemplate.Replace("${" + variable + "}", localReplacement);
            }
        }

        return resolvedVersion;
    }

    private IEnumerable<string> ExtractFailedEndpoints(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return [];
        }

        return EndpointRegex.Matches(errorMessage)
            .Select(m => m.Value)
            .Distinct();
    }

    private void LogAuthErrorGuidance()
    {
        var guidance = new StringBuilder();
        guidance.AppendLine("Maven CLI failed with authentication errors.");

        if (!this.failedEndpoints.IsEmpty)
        {
            guidance.AppendLine("The following Maven repository endpoints had authentication failures:");
            foreach (var endpoint in this.failedEndpoints.Distinct().Take(5))
            {
                guidance.AppendLine($"   - {endpoint}");
            }

            guidance.AppendLine("   Ensure your pipeline has access to these Maven repositories.");
        }

        guidance.AppendLine("Note: Falling back to static pom.xml parsing.");

        this.LogWarning(guidance.ToString());
    }

    /// <summary>
    /// Resolves parent relationships that couldn't be established during first pass.
    /// This handles cases where the parent POM was processed after the child POM.
    /// </summary>
    private void ResolveUnresolvedParentRelationships()
    {
        var resolvedCount = 0;
        var unresolvedCount = 0;

        while (this.unresolvedParentRelationships.TryDequeue(out var unresolvedRelationship))
        {
            var (filePath, parentGroupId, parentArtifactId) = unresolvedRelationship;

            // Skip if already resolved (could happen if resolved via directory traversal during first pass)
            if (this.mavenParentChildRelationships.ContainsKey(filePath))
            {
                continue;
            }

            // Clear the cache entry so we can try again with the now-complete processedMavenProjects
            this.parentPomCache.TryRemove(filePath, out _);

            // Try to find parent by coordinates now that all files have been processed
            var parentPath = this.FindParentPomByCoordinatesOnly(parentGroupId, parentArtifactId, filePath);
            if (!string.IsNullOrEmpty(parentPath))
            {
                this.mavenParentChildRelationships[filePath] = parentPath;
                resolvedCount++;
                this.Logger.LogDebug(
                    "MavenWithFallback: Resolved deferred parent relationship: {Child} → {Parent}",
                    Path.GetFileName(filePath),
                    Path.GetFileName(parentPath));
            }
            else
            {
                unresolvedCount++;
                this.Logger.LogDebug(
                    "MavenWithFallback: Could not resolve parent {ParentGroupId}:{ParentArtifactId} for {Child}",
                    parentGroupId ?? "(null)",
                    parentArtifactId,
                    Path.GetFileName(filePath));
            }
        }

        if (resolvedCount > 0 || unresolvedCount > 0)
        {
            this.LogInfo($"Resolved {resolvedCount} deferred parent relationships, {unresolvedCount} remain unresolved");
        }
    }

    /// <summary>
    /// Finds parent POM by Maven coordinates only (no directory traversal).
    /// Used for deferred parent resolution after all files have been processed.
    /// </summary>
    private string FindParentPomByCoordinatesOnly(string parentGroupId, string parentArtifactId, string currentFilePath)
    {
        if (string.IsNullOrWhiteSpace(parentArtifactId))
        {
            return string.Empty;
        }

        // Try with full coordinates first
        if (!string.IsNullOrWhiteSpace(parentGroupId))
        {
            var fullCoordinateKey = $"{parentGroupId}:{parentArtifactId}";
            if (this.processedMavenProjects.TryGetValue(fullCoordinateKey, out var fullCoordinatePath) &&
                !string.Equals(fullCoordinatePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullCoordinatePath;
            }
        }

        // Try with artifactId only
        if (this.processedMavenProjects.TryGetValue(parentArtifactId, out var artifactIdPath) &&
            !string.Equals(artifactIdPath, currentFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return artifactIdPath;
        }

        return string.Empty;
    }

    /// <summary>
    /// Third pass: resolve all pending components using hierarchy-aware variable resolution.
    /// For components with unresolved variables, this picks the closest ancestor definition
    /// based on Maven's property inheritance rules (child > parent precedence).
    /// </summary>
    private void ResolvePendingComponents()
    {
        var resolvedCount = 0;
        var skippedCount = 0;

        while (this.pendingComponents.TryDequeue(out var pendingComponent))
        {
            try
            {
                var resolvedVersion = this.ResolveVersionWithHierarchyAwareness(pendingComponent.VersionTemplate, pendingComponent.FilePath);
                if (!resolvedVersion.StartsWith("${"))
                {
                    var component = new MavenComponent(pendingComponent.GroupId, pendingComponent.ArtifactId, resolvedVersion);
                    var detectedComponent = new DetectedComponent(component);
                    pendingComponent.Recorder.RegisterUsage(detectedComponent);
                    Interlocked.Increment(ref this.staticParserComponentCount);
                    resolvedCount++;
                }
                else
                {
                    skippedCount++;
                    this.Logger.LogDebug(
                        "Version string {Version} for component {Group}/{Artifact} could not be resolved and a component will not be recorded. File: {File}",
                        resolvedVersion,
                        pendingComponent.GroupId,
                        pendingComponent.ArtifactId,
                        pendingComponent.FilePath);
                }
            }
            catch (Exception e)
            {
                skippedCount++;
                this.Logger.LogError(
                    e,
                    "Failed to resolve pending component {Group}/{Artifact} from {File}",
                    pendingComponent.GroupId,
                    pendingComponent.ArtifactId,
                    pendingComponent.FilePath);
            }
        }

        this.LogInfo($"Second pass completed: {resolvedCount} components resolved, {skippedCount} skipped due to unresolved variables");
    }

    /// <summary>
    /// Resolves a version template with hierarchy-aware precedence.
    /// When multiple variable definitions exist, picks the closest ancestor to the requesting file.
    /// This implements Maven's property inheritance rule: child properties take precedence over parent properties.
    /// </summary>
    /// <param name="versionTemplate">The version template with variables (e.g., "${revision}").</param>
    /// <param name="requestingFilePath">The file path of the POM requesting the variable resolution.</param>
    /// <returns>The resolved version string, or the original template if variables cannot be resolved.</returns>
    private string ResolveVersionWithHierarchyAwareness(string versionTemplate, string requestingFilePath)
    {
        var resolvedVersion = versionTemplate;
        var match = VersionRegex.Match(versionTemplate);

        if (match.Success)
        {
            var variable = match.Groups[1].Captures[0].ToString();

            // Use Maven-compliant hierarchy search: current → parent → grandparent
            var foundValue = this.FindVariableInMavenHierarchy(variable, requestingFilePath);
            if (foundValue != null)
            {
                resolvedVersion = versionTemplate.Replace("${" + variable + "}", foundValue.Value.Value);
            }
            else
            {
                // Variable not found in Maven hierarchy - log for debugging
                this.Logger.LogWarning(
                    "MavenWithFallback: Variable {Variable} not found in Maven hierarchy for {File}",
                    variable,
                    Path.GetFileName(requestingFilePath));
            }
        }

        return resolvedVersion;
    }

    /// <summary>
    /// Finds a variable value using Maven-compliant hierarchy search.
    /// Searches in order: current file → parent → grandparent (stops at first match).
    /// </summary>
    /// <param name="variable">Variable name to find.</param>
    /// <param name="requestingFilePath">The pom.xml file requesting the variable.</param>
    /// <returns>Variable value and source file, or null if not found in hierarchy.</returns>
    private (string Value, string SourceFile)? FindVariableInMavenHierarchy(string variable, string requestingFilePath)
    {
        var currentFile = requestingFilePath;
        var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keyBuilder = new StringBuilder(256); // Pre-allocate for typical path lengths

        // Walk up Maven parent hierarchy until variable found or no more parents
        while (!string.IsNullOrEmpty(currentFile))
        {
            // Prevent infinite loops from circular parent references
            if (!visitedFiles.Add(currentFile))
            {
                this.Logger.LogWarning(
                    "MavenWithFallback: Circular parent reference detected while resolving variable {Variable}, breaking at {File}",
                    variable,
                    Path.GetFileName(currentFile));
                break;
            }

            // Check if this file has the variable definition using StringBuilder for efficiency
            keyBuilder.Clear();
            keyBuilder.Append(currentFile).Append("::").Append(variable);
            var variableKey = keyBuilder.ToString();

            if (this.collectedVariables.TryGetValue(variableKey, out var value))
            {
                return (value, currentFile);
            }

            // Move to Maven parent (not directory parent)
            this.mavenParentChildRelationships.TryGetValue(currentFile, out currentFile);
        }

        return null; // Variable not found in Maven hierarchy
    }

    /// <summary>
    /// Filters out nested pom.xml files, keeping only root-level ones.
    /// A pom.xml is considered nested if there's another pom.xml in a parent directory.
    /// </summary>
    /// <param name="componentStreams">The incoming process requests for pom.xml files.</param>
    /// <param name="parentPomDictionary">Dictionary to populate with nested pom.xml mappings for fallback scenarios.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Process requests for only root-level pom.xml files.</returns>
    private IObservable<ProcessRequest> RemoveNestedPomXmls(
        IObservable<ProcessRequest> componentStreams,
        ConcurrentDictionary<string, IList<ProcessRequest>> parentPomDictionary,
        CancellationToken cancellationToken)
    {
        return componentStreams
            .ToList()
            .SelectMany(allRequests =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Sort all requests by path depth (parent-first) to ensure deterministic processing order.
                // This is critical for fallback static parsing where parent POMs must be processed before children
                // to ensure proper property resolution and inheritance.
                var sortedRequests = allRequests
                    .OrderBy(r => NormalizeDirectoryPath(Path.GetDirectoryName(r.ComponentStream.Location)).Length)
                    .ThenBy(r => r.ComponentStream.Location, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Use a HashSet of root directories for O(1) lookup instead of O(n) list iteration
                var rootPomDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var filteredRequests = new List<ProcessRequest>();

                foreach (var request in sortedRequests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var location = NormalizeDirectoryPath(Path.GetDirectoryName(request.ComponentStream.Location));

                    // Check if any ancestor directory is already a root POM directory
                    // Walk up the directory tree (O(depth) instead of O(n))
                    var isNested = false;
                    var parentDir = Path.GetDirectoryName(location.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                    while (!string.IsNullOrEmpty(parentDir))
                    {
                        var normalizedParent = NormalizeDirectoryPath(parentDir);
                        if (rootPomDirectories.Contains(normalizedParent))
                        {
                            this.LogDebugWithId($"Ignoring {MavenManifest} at {location}, as it has a parent {MavenManifest} at {normalizedParent}.");
                            isNested = true;
                            parentPomDictionary.AddOrUpdate(
                                normalizedParent,
                                [request],
                                (key, existingList) =>
                                {
                                    existingList.Add(request);
                                    return existingList;
                                });
                            break;
                        }

                        var nextParent = Path.GetDirectoryName(parentDir);
                        if (string.Equals(nextParent, parentDir, StringComparison.OrdinalIgnoreCase))
                        {
                            break; // Reached root
                        }

                        parentDir = nextParent;
                    }

                    if (!isNested)
                    {
                        this.LogDebugWithId($"Discovered {request.ComponentStream.Location}.");
                        rootPomDirectories.Add(location);
                        parentPomDictionary.AddOrUpdate(
                            location,
                            [request],
                            (key, existingList) =>
                            {
                                existingList.Add(request);
                                return existingList;
                            });
                        filteredRequests.Add(request);
                    }
                }

                return filteredRequests;
            });
    }
}

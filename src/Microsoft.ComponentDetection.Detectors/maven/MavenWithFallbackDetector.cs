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

    /// <summary>Maven CLI was explicitly disabled via detector argument.</summary>
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
    private readonly Dictionary<string, XmlDocument> documentsLoaded = [];

    // Track original pom.xml files for potential fallback
    private readonly ConcurrentBag<ProcessRequest> originalPomFiles = [];

    // Track Maven CLI errors for analysis
    private readonly ConcurrentBag<string> mavenCliErrors = [];
    private readonly ConcurrentBag<string> failedEndpoints = [];

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

    public override string Id => "MavenWithFallback";

    public override IList<string> SearchPatterns => [MavenManifest];

    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Maven];

    public override int Version => 1;

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Maven)];

    private void LogDebug(string message) =>
        this.Logger.LogDebug("{DetectorId}: {Message}", this.Id, message);

    private void LogInfo(string message) =>
        this.Logger.LogInformation("{DetectorId}: {Message}", this.Id, message);

    private void LogWarning(string message) =>
        this.Logger.LogWarning("{DetectorId}: {Message}", this.Id, message);

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        IDictionary<string, string> detectorArgs,
        CancellationToken cancellationToken = default)
    {
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
            this.usedDetectionMethod = MavenDetectionMethod.StaticParserOnly;
            this.fallbackReason = MavenFallbackReason.OtherMvnCliFailure;
            return processRequests;
        }
        catch (Exception ex)
        {
            // Unexpected error - log and fall back to static parsing
            this.LogWarning($"OnPrepareDetectionAsync failed with unexpected error: {ex.Message}. Falling back to static pom.xml parsing.");
            this.Telemetry["PrepareDetectionError"] = ex.GetType().Name;
            this.usedDetectionMethod = MavenDetectionMethod.StaticParserOnly;
            this.fallbackReason = MavenFallbackReason.OtherMvnCliFailure;
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

        // Run Maven CLI detection on all pom.xml files
        // Returns deps files for CLI successes, pom.xml files for CLI failures
        return await this.RunMavenCliDetectionAsync(processRequests, cancellationToken);
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

        this.LogDebug("Maven CLI is available. Running MvnCli detection.");
        return true;
    }

    /// <summary>
    /// Runs Maven CLI detection on all root pom.xml files.
    /// For each pom.xml, if CLI succeeds, the deps file is added to results.
    /// If CLI fails, all pom.xml files under that directory are added for static parsing fallback.
    /// </summary>
    /// <param name="processRequests">The incoming process requests.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An observable of process requests (deps files for CLI success, pom.xml for CLI failure).</returns>
    private async Task<IObservable<ProcessRequest>> RunMavenCliDetectionAsync(
        IObservable<ProcessRequest> processRequests,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<ProcessRequest>();
        var failedDirectories = new ConcurrentBag<string>();
        var cliSuccessCount = 0;
        var cliFailureCount = 0;

        // Use unbounded parallelism to match MvnCliComponentDetector behavior.
        // Maven CLI invocations are I/O bound and benefit from parallel execution.
        var processPomFile = new ActionBlock<ProcessRequest>(async processRequest =>
        {
            // Store original pom.xml for telemetry
            this.originalPomFiles.Add(processRequest);

            var pomFile = processRequest.ComponentStream;
            var pomDir = Path.GetDirectoryName(pomFile.Location);
            var depsFileName = this.mavenCommandService.BcdeMvnDependencyFileName;
            var depsFilePath = Path.Combine(pomDir, depsFileName);

            // Generate dependency file using Maven CLI.
            // Note: If both MvnCliComponentDetector and this detector are enabled,
            // they may run Maven CLI on the same pom.xml independently.
            var result = await this.mavenCommandService.GenerateDependenciesFileAsync(
                processRequest,
                depsFileName,
                cancellationToken);

            if (result.Success)
            {
                // CLI succeeded - read the generated deps file
                // We read the file here (rather than in MavenCommandService) to avoid
                // unnecessary I/O for callers like MvnCliComponentDetector that scan for files later.
                string depsFileContent = null;
                if (this.fileUtilityService.Exists(depsFilePath))
                {
                    depsFileContent = this.fileUtilityService.ReadAllText(depsFilePath);
                }

                if (!string.IsNullOrEmpty(depsFileContent))
                {
                    Interlocked.Increment(ref cliSuccessCount);
                    results.Add(new ProcessRequest
                    {
                        ComponentStream = new ComponentStream
                        {
                            Stream = new MemoryStream(Encoding.UTF8.GetBytes(depsFileContent)),
                            Location = depsFilePath,
                            Pattern = depsFileName,
                        },
                        SingleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(
                            Path.Combine(pomDir, MavenManifest)),
                    });
                }
                else
                {
                    // CLI reported success but deps file is missing or empty - treat as failure
                    Interlocked.Increment(ref cliFailureCount);
                    failedDirectories.Add(pomDir);
                    this.LogWarning($"Maven CLI succeeded but deps file not found or empty: {depsFilePath}");
                }
            }
            else
            {
                // CLI failed - track directory for nested pom.xml scanning
                Interlocked.Increment(ref cliFailureCount);
                failedDirectories.Add(pomDir);

                // Capture error output for later analysis
                if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                {
                    this.mavenCliErrors.Add(result.ErrorOutput);
                }
            }
        });

        await this.RemoveNestedPomXmls(processRequests).ForEachAsync(processRequest =>
        {
            processPomFile.Post(processRequest);
        });

        processPomFile.Complete();
        await processPomFile.Completion;

        // For failed directories, scan and add all pom.xml files for static parsing
        if (!failedDirectories.IsEmpty)
        {
            var staticParsingRequests = this.GetAllPomFilesInDirectories(failedDirectories.ToHashSet(StringComparer.OrdinalIgnoreCase));
            foreach (var request in staticParsingRequests)
            {
                results.Add(request);
            }
        }

        // Determine detection method based on results
        this.DetermineDetectionMethod(cliSuccessCount, cliFailureCount);

        this.LogDebug($"Maven CLI processing complete: {cliSuccessCount} succeeded, {cliFailureCount} failed out of {this.originalPomFiles.Count} root pom.xml files.");

        return results.ToObservable();
    }

    /// <summary>
    /// Gets all pom.xml files in the specified directories and their subdirectories for static parsing.
    /// </summary>
    /// <param name="directories">The directories to scan for pom.xml files.</param>
    /// <returns>ProcessRequests for all pom.xml files in the specified directories.</returns>
    private IEnumerable<ProcessRequest> GetAllPomFilesInDirectories(HashSet<string> directories)
    {
        this.LogDebug($"Scanning for pom.xml files in {directories.Count} failed directories for static parsing fallback.");

        // Normalize directories once for efficient lookup
        var normalizedDirs = directories
            .Select(d => d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToList();

        return this.ComponentStreamEnumerableFactory
            .GetComponentStreams(
                this.CurrentScanRequest.SourceDirectory,
                [MavenManifest],
                this.CurrentScanRequest.DirectoryExclusionPredicate)
            .Where(componentStream =>
            {
                var fileDir = Path.GetDirectoryName(componentStream.Location);
                var normalizedFileDir = fileDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                // Include if this file is in or under any failed directory
                // Use pre-normalized directories for efficient comparison
                return normalizedDirs.Any(fd =>
                    normalizedFileDir.Equals(fd, StringComparison.OrdinalIgnoreCase) ||
                    normalizedFileDir.StartsWith(fd, StringComparison.OrdinalIgnoreCase));
            })
            .Select(componentStream =>
            {
                using var reader = new StreamReader(componentStream.Stream);
                var content = reader.ReadToEnd();
                return new ProcessRequest
                {
                    ComponentStream = new ComponentStream
                    {
                        Stream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                        Location = componentStream.Location,
                        Pattern = MavenManifest,
                    },
                    SingleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(componentStream.Location),
                };
            })
            .ToList();
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
            this.LogDebug("All pom.xml files processed successfully with Maven CLI.");
        }
        else if (cliSuccessCount == 0 && cliFailureCount > 0)
        {
            this.usedDetectionMethod = MavenDetectionMethod.StaticParserOnly;
            this.LogWarning("Maven CLI failed for all pom.xml files. Using static parsing fallback.");
            this.AnalyzeMvnCliFailure();
        }
        else if (cliSuccessCount > 0 && cliFailureCount > 0)
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
        // Record telemetry
        this.Telemetry["DetectionMethod"] = this.usedDetectionMethod.ToString();
        this.Telemetry["FallbackReason"] = this.fallbackReason.ToString();
        this.Telemetry["MvnCliComponentCount"] = this.mvnCliComponentCount.ToString();
        this.Telemetry["StaticParserComponentCount"] = this.staticParserComponentCount.ToString();
        this.Telemetry["TotalComponentCount"] = (this.mvnCliComponentCount + this.staticParserComponentCount).ToString();
        this.Telemetry["MavenCliAvailable"] = this.mavenCliAvailable.ToString();
        this.Telemetry["OriginalPomFileCount"] = this.originalPomFiles.Count.ToString();

        if (!this.failedEndpoints.IsEmpty)
        {
            this.Telemetry["FailedEndpoints"] = string.Join(";", this.failedEndpoints.Distinct().Take(10));
        }

        this.LogInfo($"Detection completed. Method: {this.usedDetectionMethod}, " +
                     $"FallbackReason: {this.fallbackReason}, " +
                     $"MvnCli components: {this.mvnCliComponentCount}, " +
                     $"Static parser components: {this.staticParserComponentCount}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyzes Maven CLI failure by checking logged errors for authentication issues.
    /// </summary>
    private void AnalyzeMvnCliFailure()
    {
        // Check if any recorded errors indicate authentication failure
        var hasAuthError = this.mavenCliErrors.Any(this.IsAuthenticationError);

        if (hasAuthError)
        {
            this.fallbackReason = MavenFallbackReason.AuthenticationFailure;

            // Extract failed endpoints from error messages
            foreach (var endpoint in this.mavenCliErrors.SelectMany(this.ExtractFailedEndpoints))
            {
                this.failedEndpoints.Add(endpoint);
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
        var initialCount = this.ComponentRecorder.GetDetectedComponents().Count();

        this.mavenCommandService.ParseDependenciesFile(processRequest);

        // Try to delete the deps file
        try
        {
            File.Delete(processRequest.ComponentStream.Location);
        }
        catch
        {
            // Ignore deletion errors
        }

        var newCount = this.ComponentRecorder.GetDetectedComponents().Count();
        Interlocked.Add(ref this.mvnCliComponentCount, newCount - initialCount);
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

            lock (this.documentsLoaded)
            {
                this.documentsLoaded.TryAdd(file.Location, document);
            }

            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace(ProjNamespace, MavenXmlNamespace);

            var dependencyList = document.SelectNodes(DependencyNode, namespaceManager);
            var componentsFoundInFile = 0;

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
                    string versionString;

                    if (versionRef.StartsWith("${"))
                    {
                        versionString = this.ResolveVersion(versionRef, document, file.Location);
                    }
                    else
                    {
                        versionString = versionRef;
                    }

                    if (!versionString.StartsWith("${"))
                    {
                        var component = new MavenComponent(groupId, artifactId, versionString);
                        var detectedComponent = new DetectedComponent(component);
                        singleFileComponentRecorder.RegisterUsage(detectedComponent);
                        componentsFoundInFile++;
                    }
                    else
                    {
                        this.Logger.LogDebug(
                            "Version string {Version} for component {Group}/{Artifact} is invalid or unsupported and a component will not be recorded.",
                            versionString,
                            groupId,
                            artifactId);
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

            Interlocked.Add(ref this.staticParserComponentCount, componentsFoundInFile);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read file {Path}", filePath);
        }
    }

    private bool IsAuthenticationError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        return AuthErrorPatterns.Any(pattern =>
            errorMessage.Contains(pattern, StringComparison.OrdinalIgnoreCase));
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

    private string ResolveVersion(string versionString, XmlDocument currentDocument, string currentDocumentFileLocation)
    {
        var returnedVersionString = versionString;
        var match = VersionRegex.Match(versionString);

        if (match.Success)
        {
            var variable = match.Groups[1].Captures[0].ToString();
            var replacement = this.ReplaceVariable(variable, currentDocument, currentDocumentFileLocation);
            returnedVersionString = versionString.Replace("${" + variable + "}", replacement);
        }

        return returnedVersionString;
    }

    private string ReplaceVariable(string variable, XmlDocument currentDocument, string currentDocumentFileLocation)
    {
        var result = this.FindVariableInDocument(currentDocument, currentDocumentFileLocation, variable);
        if (result != null)
        {
            return result;
        }

        lock (this.documentsLoaded)
        {
            foreach (var pathDocumentPair in this.documentsLoaded)
            {
                var path = pathDocumentPair.Key;
                var document = pathDocumentPair.Value;
                result = this.FindVariableInDocument(document, path, variable);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return $"${{{variable}}}";
    }

    private string FindVariableInDocument(XmlDocument document, string path, string variable)
    {
        try
        {
            var namespaceManager = new XmlNamespaceManager(document.NameTable);
            namespaceManager.AddNamespace(ProjNamespace, MavenXmlNamespace);

            var nodeListProject = document.SelectNodes($"//proj:{variable}", namespaceManager);
            var nodeListProperties = document.SelectNodes($"//proj:properties/proj:{variable}", namespaceManager);

            if (nodeListProject.Count != 0)
            {
                return nodeListProject.Item(0).InnerText;
            }

            if (nodeListProperties.Count != 0)
            {
                return nodeListProperties.Item(0).InnerText;
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Failed to read file {Path}", path);
        }

        return null;
    }

    /// <summary>
    /// Filters out nested pom.xml files, keeping only root-level ones.
    /// A pom.xml is considered nested if there's another pom.xml in a parent directory.
    /// </summary>
    /// <param name="componentStreams">The incoming process requests for pom.xml files.</param>
    /// <returns>Process requests for only root-level pom.xml files.</returns>
    private IObservable<ProcessRequest> RemoveNestedPomXmls(IObservable<ProcessRequest> componentStreams)
    {
        return componentStreams
            .ToList()
            .SelectMany(allRequests =>
            {
                // Build a list of all directories that contain a pom.xml, ordered by path length (shortest first).
                // This ensures parent directories are checked before their children.
                var pomDirectories = allRequests
                    .Select(r => NormalizeDirectoryPath(Path.GetDirectoryName(r.ComponentStream.Location)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(d => d.Length)
                    .ToList();

                return allRequests.Where(request =>
                {
                    var location = NormalizeDirectoryPath(Path.GetDirectoryName(request.ComponentStream.Location));

                    foreach (var pomDirectory in pomDirectories)
                    {
                        if (pomDirectory.Length >= location.Length)
                        {
                            // Since the list is ordered by length, if the pomDirectory is longer than
                            // or equal to the location, there are no possible parent directories left.
                            break;
                        }

                        if (location.StartsWith(pomDirectory, StringComparison.OrdinalIgnoreCase))
                        {
                            this.LogDebug($"Ignoring {MavenManifest} at {location}, as it has a parent {MavenManifest} at {pomDirectory}.");
                            return false;
                        }
                    }

                    this.LogDebug($"Discovered {request.ComponentStream.Location}.");
                    return true;
                });
            });

        // Normalizes a directory path by ensuring it ends with a directory separator.
        // This prevents false matches like "C:\foo" matching "C:\foobar".
        static string NormalizeDirectoryPath(string path) =>
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }
}

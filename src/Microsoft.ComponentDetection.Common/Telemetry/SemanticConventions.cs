namespace Microsoft.ComponentDetection.Common.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry semantic conventions for Component Detection telemetry.
/// Includes standard OTel conventions and custom azuredevops.* namespace attributes.
/// </summary>
public static class SemanticConventions
{
    /// <summary>
    /// ActivitySource for distributed tracing in Component Detection.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("component-detection");

    /// <summary>
    /// Meter for metrics in Component Detection.
    /// </summary>
    public static readonly Meter Meter = new("component-detection.metrics");

    /// <summary>
    /// Standard OpenTelemetry semantic convention attributes.
    /// </summary>
    public static class Attributes
    {
        // Process attributes
        public const string ProcessExitCode = "process.exit.code";

        // Code attributes
        public const string CodeFunction = "code.function";
        public const string CodeNamespace = "code.namespace";

        // Service attributes
        public const string ServiceName = "service.name";
        public const string ServiceVersion = "service.version";
        public const string ServiceInstanceId = "service.instance.id";

        // Error attributes
        public const string ErrorType = "error.type";
        public const string ErrorMessage = "error.message";
        public const string ErrorStackTrace = "error.stack_trace";

        // Thread attributes
        public const string ThreadId = "thread.id";
        public const string ThreadName = "thread.name";
    }

    /// <summary>
    /// Component Detection specific attributes.
    /// </summary>
    public static class ComponentDetection
    {
        public const string DetectorId = "detector.id";
        public const string DetectorCategory = "detector.category";
        public const string ComponentCount = "component.count";
        public const string ComponentType = "component.type";
        public const string ScanType = "scan.type";
        public const string SourceDirectory = "source.directory";
        public const string Timeout = "timeout.seconds";
        public const string MaxThreads = "max.threads";
    }

    /// <summary>
    /// Azure DevOps specific semantic conventions.
    /// Custom namespace for AzDO-specific telemetry attributes.
    /// </summary>
    public static class AzureDevOps
    {
        // Build attributes
        public const string BuildId = "azuredevops.build.id";
        public const string BuildNumber = "azuredevops.build.number";
        public const string BuildDefinitionId = "azuredevops.build.definition_id";
        public const string BuildDefinitionName = "azuredevops.build.definition_name";
        public const string BuildProjectId = "azuredevops.build.project_id";
        public const string BuildProjectName = "azuredevops.build.project_name";
        public const string BuildUri = "azuredevops.build.uri";
        public const string BuildReason = "azuredevops.build.reason";

        // Repository attributes
        public const string RepositoryId = "azuredevops.repository.id";
        public const string RepositoryName = "azuredevops.repository.name";
        public const string RepositoryProjectId = "azuredevops.repository.project_id";
        public const string RepositoryProvider = "azuredevops.repository.provider";
        public const string RepositoryUri = "azuredevops.repository.uri";
        public const string RepositoryBranch = "azuredevops.repository.branch";

        // Source/Commit attributes
        public const string SourceCommitId = "azuredevops.source.commit_id";
        public const string SourceCommitIdShort = "azuredevops.source.commit_id_short";

        // Pipeline attributes
        public const string PipelineId = "azuredevops.pipeline.id";
        public const string PhaseId = "azuredevops.phase.id";
        public const string PhaseDisplayName = "azuredevops.phase.display_name";
        public const string JobAttempt = "azuredevops.job.attempt";
        public const string StageAttempt = "azuredevops.stage.attempt";

        // Pull Request attributes
        public const string PullRequestId = "azuredevops.pull_request.id";

        // Organization attributes
        public const string OrganizationId = "azuredevops.organization.id";
        public const string CollectionId = "azuredevops.collection.id";

        // Alert attributes
        public const string AlertCount = "azuredevops.alert.count";
        public const string AlertSeverity = "azuredevops.alert.severity";
        public const string AlertWarningLevel = "azuredevops.alert.warning_level";

        // Governance attributes
        public const string GovernanceUrl = "azuredevops.governance.url";
        public const string AutoInjected = "azuredevops.auto_injected";

        // Task attributes
        public const string TaskVersion = "azuredevops.task.version";
        public const string TaskInput = "azuredevops.task.input";
        public const string CallerIdentifier = "azuredevops.caller.identifier";
        public const string RunSource = "azuredevops.run.source";

        // Agent attributes
        public const string AgentOsMeaningfulDetails = "azuredevops.agent.os.meaningful_details";
        public const string AgentOsDescription = "azuredevops.agent.os.description";

        // Detection attributes
        public const string DetectionVersion = "azuredevops.detection.version";
        public const string DetectionArguments = "azuredevops.detection.arguments";
        public const string DetectionHiddenExitCode = "azuredevops.detection.hidden_exit_code";
    }

    /// <summary>
    /// Metric names for Component Detection.
    /// </summary>
    public static class Metrics
    {
        public const string ComponentsDetected = "components.detected";
        public const string DetectorExecutionDuration = "detector.execution.duration";
        public const string DetectorExecutionCount = "detector.execution.count";
        public const string AlertsCreated = "alerts.created";
        public const string ScanDuration = "scan.duration";
        public const string FilesParsed = "files.parsed";
        public const string FilesSkipped = "files.skipped";
        public const string RegistrationCount = "registrations.count";
    }
}

#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Text.Json.Serialization;

public class DotNetComponent : TypedComponent
{
    private const string UnknownValue = "unknown";

    private DotNetComponent()
    {
        /* Reserved for deserialization */
    }

    public DotNetComponent(string sdkVersion, string targetFramework = null, string projectType = null)
    {
        if (string.IsNullOrWhiteSpace(sdkVersion) && string.IsNullOrWhiteSpace(targetFramework))
        {
            throw new ArgumentNullException(nameof(sdkVersion), $"Either {nameof(sdkVersion)} or {nameof(targetFramework)} of component type {nameof(DotNetComponent)} must be specified.");
        }

        this.SdkVersion = string.IsNullOrWhiteSpace(sdkVersion) ? UnknownValue : sdkVersion;
        this.TargetFramework = string.IsNullOrWhiteSpace(targetFramework) ? UnknownValue : targetFramework;
        this.ProjectType = string.IsNullOrWhiteSpace(projectType) ? UnknownValue : projectType;
    }

    /// <summary>
    /// SDK Version detected, could be null if no global.json exists and no dotnet is on the path.
    /// </summary>
    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; }

    /// <summary>
    /// Target framework for this instance.  Null in the case of global.json.
    /// </summary>
    [JsonPropertyName("targetFramework")]
    public string TargetFramework { get; set; }

    /// <summary>
    /// Project type: application, library.  Null in the case of global.json or if no project output could be discovered.
    /// </summary>
    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; }

    public override ComponentType Type => ComponentType.DotNet;

    /// <summary>
    /// Provides an id like `{SdkVersion} - {TargetFramework} - {ProjectType} - dotnet` where unspecified values are represented as 'unknown'.
    /// </summary>
    /// <returns>Id of the component.</returns>
    protected override string ComputeId() => $"{this.SdkVersion} {this.TargetFramework} {this.ProjectType} - {this.Type}";
}

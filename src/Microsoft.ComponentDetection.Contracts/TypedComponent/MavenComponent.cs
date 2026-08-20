#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

public class MavenComponent : TypedComponent
{
    public MavenComponent(string groupId, string artifactId, string version)
    {
        this.GroupId = this.ValidateRequiredInput(groupId, nameof(this.GroupId), nameof(ComponentType.Maven));
        this.ArtifactId = this.ValidateRequiredInput(artifactId, nameof(this.ArtifactId), nameof(ComponentType.Maven));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Maven));
    }

    public MavenComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("groupId")]
    public string GroupId { get; set; }

    [JsonPropertyName("artifactId")]
    public string ArtifactId { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Maven;

    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl => new PackageURL("maven", this.GroupId, this.ArtifactId, this.Version, null, null);

    protected override string ComputeBaseId() => $"{this.GroupId} {this.ArtifactId} {this.Version} - {this.Type}";

    /// <summary>
    /// Suppresses the base impl so <see cref="TypedComponent.Id"/> stays stable if a detector later
    /// populates <see cref="TypedComponent.DownloadUrl"/> or <see cref="TypedComponent.SourceUrl"/>.
    /// GroupId/ArtifactId/Version are already in BaseId; the Maven Central download URL is deterministic
    /// and source/repo URLs are surfaced server-side from the POM.
    /// </summary>
    /// <returns>An empty sequence.</returns>
    protected override IEnumerable<KeyValuePair<string, string>> GetExtendedIdProperties()
    {
        yield break;
    }
}
